using System.Runtime.ExceptionServices;

namespace CoffeeShop.Tests;

public static class StaTestRunner
{
    public static void Run(Action action, int timeoutSeconds = 30)
    {
        RunAsync(_ =>
        {
            action();
            return Task.CompletedTask;
        }, timeoutSeconds);
    }

    public static void RunAsync(Func<Action<string>, Task> action, int timeoutSeconds = 30)
    {
        ExceptionDispatchInfo? captured = null;
        string currentStep = "starting";
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var stepLog = new List<string>();
        using var done = new ManualResetEventSlim(false);

        void RecordStep(string s)
        {
            var entry = $"{sw.ElapsedMilliseconds}ms:{s}";
            Volatile.Write(ref currentStep, entry);
            lock (stepLog) stepLog.Add(entry);
        }

        string FormatHistory()
        {
            lock (stepLog) return string.Join(" -> ", stepLog);
        }

        var thread = new Thread(() =>
        {
            System.Windows.Forms.Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException, threadScope: true);
            WindowsFormsSynchronizationContext.AutoInstall = false;
            var appCtx = new ApplicationContext();
            var syncCtx = new WindowsFormsSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(syncCtx);

            using var timer = new System.Windows.Forms.Timer { Interval = timeoutSeconds * 1000 };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                if (captured is null)
                {
                    captured = ExceptionDispatchInfo.Capture(
                        new TimeoutException($"WinForms STA test timed out after {timeoutSeconds}s at '{Volatile.Read(ref currentStep)}'. Trace: [{FormatHistory()}]"));
                }
                appCtx.ExitThread();
            };
            timer.Start();

            syncCtx.Post(async _ =>
            {
                try
                {
                    await action(RecordStep);
                }
                catch (Exception ex)
                {
                    captured ??= ExceptionDispatchInfo.Capture(ex);
                }
                finally
                {
                    timer.Stop();
                    appCtx.ExitThread();
                }
            }, null);

            try
            {
                System.Windows.Forms.Application.Run(appCtx);
            }
            catch (Exception ex) when (captured is null)
            {
                captured = ExceptionDispatchInfo.Capture(ex);
            }
            finally
            {
                timer.Stop();
                appCtx.Dispose();
                SynchronizationContext.SetSynchronizationContext(null);
                syncCtx.Dispose();
                done.Set();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();

        if (!done.Wait(TimeSpan.FromSeconds(timeoutSeconds + 5)))
            throw new TimeoutException($"WinForms STA thread did not complete within {timeoutSeconds + 5}s (stuck at '{Volatile.Read(ref currentStep)}'). Trace: [{FormatHistory()}]");

        captured?.Throw();
    }

    public static void RunAsync(Func<Task> action, int timeoutSeconds = 30)
    {
        RunAsync(_ => action(), timeoutSeconds);
    }

    public static async Task YieldUiAsync(int delayMs = 20)
    {
        await Task.Delay(delayMs);
    }

    public static void PumpEvents(int rounds = 3)
    {
        for (var i = 0; i < rounds; i++)
        {
            System.Windows.Forms.Application.DoEvents();
            Thread.Sleep(5);
        }
    }
}
