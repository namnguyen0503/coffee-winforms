using CoffeeShop.Application.Common;
using CoffeeShop.Application.DTOs;
using CoffeeShop.Application.Interfaces;
using CoffeeShop.Domain.Enums;
using CoffeeShop.WinForms.Controls;
using CoffeeShop.WinForms.Forms;
using CoffeeShop.WinForms.Helpers;
using CoffeeShop.WinForms.Theme;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeShop.Tests;

[Collection("WinFormsUI")]
public class WinFormsUiTests
{
    private static Form CreateOffScreenHost(Control child, int width = 1280, int height = 760)
    {
        var host = new Form
        {
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-20000, -20000),
            Size = new Size(width, height)
        };
        child.Dock = DockStyle.Fill;
        host.Controls.Add(child);
        return host;
    }

    private static void ConfigureOffScreenForm(Form form)
    {
        form.ShowInTaskbar = false;
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-20000, -20000);
    }

    private static List<T> FindControls<T>(Control root) where T : Control
    {
        var list = new List<T>();
        foreach (Control child in root.Controls)
        {
            if (child is T typed)
                list.Add(typed);
            if (child.HasChildren)
                list.AddRange(FindControls<T>(child));
        }
        return list;
    }

    [Fact]
    public void CustomerDisplayForm_SplitContainerResizeAndStates_NeverThrow()
    {
        StaTestRunner.Run(() =>
        {
            var settings = new AppSettings();
            using var form = new CustomerDisplayForm(settings);
            ConfigureOffScreenForm(form);
            form.Show();
            StaTestRunner.PumpEvents();

            form.ShowTransferPayment(125000m, "TEST MEMO");
            form.UpdateCart(
            [
                new CustomerDisplayItem("Cà phê Đen", 2, 25000m, "Ít đá", "Assets/Images/drink/ca-phe-den.jpg"),
                new CustomerDisplayItem("Latte Đá", 1, 45000m, null, "Assets/Images/drink/latte-da.jpg")
            ], 95000m, 9500m, 85500m);
            form.ShowPaymentSuccess();
            form.ShowWelcome();
            form.UpdateCart([], 0m, 0m, 0m);

            // Resize across normal and extreme tiny sizes (clearing MinimumSize for stress test)
            form.Size = new Size(1200, 800);
            StaTestRunner.PumpEvents(2);
            form.Size = new Size(760, 480);
            StaTestRunner.PumpEvents(2);
            form.MinimumSize = Size.Empty;
            form.Size = new Size(420, 300);
            StaTestRunner.PumpEvents(2);
            form.Size = new Size(220, 180);
            StaTestRunner.PumpEvents(2);
            form.Close();
        });
    }

    [Fact]
    public void PosControl_LoadInteractiveCartVoucherAndExtremeResize_NeverThrows()
    {
        StaTestRunner.RunAsync(async step =>
        {
            step("PosControl init");
            await using var fx = new TestDbFixture();
            await fx.InitializeAndSeedAsync();
            await using var sp = fx.CreateServiceProvider();

            var authSvc = sp.GetRequiredService<IAuthService>();
            var shiftSvc = sp.GetRequiredService<IShiftService>();
            var session = sp.GetRequiredService<AppSession>();

            var login = await authSvc.LoginAsync("admin", "123456", DateTime.Today.AddHours(10));
            session.SetUser(login.User!);
            var started = await shiftSvc.StartShiftAsync(login.User!.Id, 500000m);
            session.SetActiveSession(started.Data);

            using var pos = new PosControl(
                sp.GetRequiredService<IMenuService>(),
                sp.GetRequiredService<IOrderService>(),
                sp.GetRequiredService<IVoucherService>(),
                shiftSvc,
                session,
                sp.GetRequiredService<AppSettings>(),
                () => { });

            using var host = CreateOffScreenHost(pos);
            host.Show();
            await StaTestRunner.YieldUiAsync();

            step("PosControl LoadAsync");
            await pos.LoadAsync(promptStartShift: true);
            await StaTestRunner.YieldUiAsync();

            step("PosControl Resize");
            foreach (var size in new[] { new Size(1600, 900), new Size(1200, 720), new Size(700, 500), new Size(250, 200), new Size(1280, 760) })
            {
                host.Size = size;
                await StaTestRunner.YieldUiAsync(15);
            }

            step("PosControl Cart & Filter");
            var productCards = FindControls<Panel>(pos).Where(p => p.Size == new Size(164, 212)).ToList();
            Assert.NotEmpty(productCards);

            var textBoxes = FindControls<TextBox>(pos);
            var txtSearch = textBoxes.First(tb => tb.PlaceholderText.Contains("Tìm nhanh"));
            var txtVoucher = textBoxes.First(tb => tb.PlaceholderText.Contains("Mã giảm giá"));

            txtSearch.Text = "Latte";
            await StaTestRunner.YieldUiAsync(15);
            Assert.Contains(productCards, c => c.Visible);
            Assert.Contains(productCards, c => !c.Visible);

            txtSearch.Text = "";
            await StaTestRunner.YieldUiAsync(15);
            Assert.All(productCards, c => Assert.True(c.Visible));

            var onClick = typeof(Control).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            onClick.Invoke(productCards[0], [EventArgs.Empty]);

            List<Panel> GetCartRows() => FindControls<Panel>(pos).Where(p => Equals(p.Tag, "cart-row")).ToList();

            var cartRows = GetCartRows();
            Assert.Single(cartRows);
            Assert.Equal("1", FindControls<TextBox>(cartRows[0]).Single(t => Equals(t.Tag, "qty")).Text);

            // Inline '+' button
            FindControls<Button>(cartRows[0]).Single(b => Equals(b.Tag, "plus")).PerformClick();
            cartRows = GetCartRows();
            Assert.Equal("2", FindControls<TextBox>(cartRows[0]).Single(t => Equals(t.Tag, "qty")).Text);

            // Inline direct qty edit ('3')
            var txtQty = FindControls<TextBox>(cartRows[0]).Single(t => Equals(t.Tag, "qty"));
            txtQty.Text = "3";
            Assert.Equal("3", txtQty.Text);

            // Inline '-' button -> back to 2
            FindControls<Button>(cartRows[0]).Single(b => Equals(b.Tag, "minus")).PerformClick();
            cartRows = GetCartRows();
            Assert.Equal("2", FindControls<TextBox>(cartRows[0]).Single(t => Equals(t.Tag, "qty")).Text);

            // Inline note edit
            var txtInlineNote = FindControls<TextBox>(cartRows[0]).Single(t => Equals(t.Tag, "note"));
            txtInlineNote.Text = "Ít đá";

            var allButtons = FindControls<Button>(pos);
            allButtons.Single(b => b.Text == "Xóa hết").PerformClick();
            Assert.Empty(GetCartRows());

            onClick.Invoke(productCards[0], [EventArgs.Empty]);
            Assert.Single(GetCartRows());

            step("PosControl CustomerDisplay");
            allButtons.Single(b => b.Text == "Màn hình khách").PerformClick();
            await StaTestRunner.YieldUiAsync();

            foreach (Form openForm in System.Windows.Forms.Application.OpenForms)
            {
                if (openForm is CustomerDisplayForm cust)
                    cust.Location = new Point(-20000, -20000);
            }

            var catBtn = allButtons.FirstOrDefault(b => b.Text == "Đồ uống");
            Assert.NotNull(catBtn);
            catBtn!.PerformClick();
            await StaTestRunner.YieldUiAsync(15);
            Assert.Contains(productCards, c => c.Visible);
            Assert.Contains(productCards, c => !c.Visible);

            allButtons.Single(b => b.Text == "Tất cả").PerformClick();
            await StaTestRunner.YieldUiAsync(15);
            Assert.All(productCards, c => Assert.True(c.Visible));

            step("PosControl Voucher");
            txtVoucher.Text = "INVALID99";
            var btnApply = allButtons.Single(b => b.Text == "Áp dụng");
            btnApply.PerformClick();
            await StaTestRunner.YieldUiAsync(50);

            txtVoucher.Text = "WELCOME";
            btnApply.PerformClick();
            await StaTestRunner.YieldUiAsync(50);

            host.Close();
        }, timeoutSeconds: 30);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AllUserControls_LoadAndSwitchTabsSafely(bool seeded)
    {
        StaTestRunner.RunAsync(async step =>
        {
            step($"init seeded={seeded}");
            await using var fx = new TestDbFixture();
            if (seeded) await fx.InitializeAndSeedAsync();
            else await fx.InitializeEmptyAsync();

            await using var sp = fx.CreateServiceProvider();
            var session = sp.GetRequiredService<AppSession>();

            if (seeded)
            {
                var login = await sp.GetRequiredService<IAuthService>().LoginAsync("admin", "123456", DateTime.Today.AddHours(10));
                session.SetUser(login.User!);
            }
            else
            {
                session.SetUser(new UserDto(1, "Fallback Admin", "admin", UserRole.Admin, true, DateTime.Now));
            }

            // DashboardControl
            step($"Dashboard seeded={seeded}");
            using (var dash = new DashboardControl(sp.GetRequiredService<IReportService>(), sp.GetRequiredService<IShiftService>(), session, _ => { }))
            using (var host = CreateOffScreenHost(dash))
            {
                host.Show();
                await dash.LoadAsync();
                await StaTestRunner.YieldUiAsync();
                host.Close();
            }

            // PosControl (no modal prompt)
            step($"PosControl seeded={seeded}");
            using (var pos = new PosControl(sp.GetRequiredService<IMenuService>(), sp.GetRequiredService<IOrderService>(), sp.GetRequiredService<IVoucherService>(), sp.GetRequiredService<IShiftService>(), session, sp.GetRequiredService<AppSettings>(), () => { }))
            using (var host = CreateOffScreenHost(pos))
            {
                host.Show();
                await pos.LoadAsync(promptStartShift: false);
                await StaTestRunner.YieldUiAsync();
                host.Close();
            }

            // OrdersControl
            step($"OrdersControl seeded={seeded}");
            using (var orders = new OrdersControl(sp.GetRequiredService<IOrderService>(), sp.GetRequiredService<ICsvExportService>(), session))
            using (var host = CreateOffScreenHost(orders))
            {
                host.Show();
                await orders.LoadAsync();
                await StaTestRunner.YieldUiAsync();
                host.Close();
            }

            // MenuManagementControl (tests ComboBox DataSource/DisplayMember binding order & _isLoading guard)
            step($"MenuManagementControl seeded={seeded}");
            using (var menu = new MenuManagementControl(sp.GetRequiredService<IMenuService>(), sp.GetRequiredService<IInventoryService>()))
            using (var host = CreateOffScreenHost(menu))
            {
                host.Show();
                await menu.LoadAsync();
                await StaTestRunner.YieldUiAsync();
                var cbo = FindControls<ComboBox>(menu).FirstOrDefault();
                if (cbo is not null && cbo.Items.Count > 1)
                {
                    cbo.SelectedIndex = 1;
                    await StaTestRunner.YieldUiAsync(30);
                }
                host.Close();
            }

            // WarehouseControl (tests tab switch & log filter before/after handle creation)
            step($"WarehouseControl seeded={seeded}");
            using (var wh = new WarehouseControl(sp.GetRequiredService<IInventoryService>(), sp.GetRequiredService<IUserScheduleService>(), sp.GetRequiredService<ICsvExportService>(), session))
            using (var host = CreateOffScreenHost(wh))
            {
                host.Show();
                await wh.LoadAsync();
                await StaTestRunner.YieldUiAsync();
                var tabs = FindControls<TabControl>(wh).Single();
                tabs.SelectedIndex = 1;
                await StaTestRunner.YieldUiAsync(30);
                tabs.SelectedIndex = 0;
                await StaTestRunner.YieldUiAsync();
                host.Close();
            }

            // VouchersControl
            step($"VouchersControl seeded={seeded}");
            using (var vouchers = new VouchersControl(sp.GetRequiredService<IVoucherService>()))
            using (var host = CreateOffScreenHost(vouchers))
            {
                host.Show();
                await vouchers.LoadAsync();
                await StaTestRunner.YieldUiAsync();
                if (seeded)
                {
                    var btnToggle = FindControls<Button>(vouchers).Single(b => b.Text == "Bật / Tắt mã");
                    btnToggle.PerformClick();
                    await StaTestRunner.YieldUiAsync(30);
                }
                host.Close();
            }

            // UsersSchedulesControl
            step($"UsersSchedulesControl seeded={seeded}");
            using (var users = new UsersSchedulesControl(sp.GetRequiredService<IUserScheduleService>()))
            using (var host = CreateOffScreenHost(users))
            {
                host.Show();
                await users.LoadAsync();
                await StaTestRunner.YieldUiAsync();
                var tabs = FindControls<TabControl>(users).Single();
                tabs.SelectedIndex = 1;
                await StaTestRunner.YieldUiAsync();
                host.Close();
            }

            // ReportsControl
            step($"ReportsControl seeded={seeded}");
            using (var reports = new ReportsControl(sp.GetRequiredService<IReportService>(), sp.GetRequiredService<IShiftService>(), sp.GetRequiredService<ICsvExportService>()))
            using (var host = CreateOffScreenHost(reports))
            {
                host.Show();
                await reports.LoadAsync();
                await StaTestRunner.YieldUiAsync();
                var tabs = FindControls<TabControl>(reports).Single();
                tabs.SelectedIndex = 1;
                await StaTestRunner.YieldUiAsync();
                host.Close();
            }
        }, timeoutSeconds: 30);
    }

    [Fact]
    public void MainForm_AllRolesNavigateAndResize_WithoutErrors()
    {
        StaTestRunner.RunAsync(async step =>
        {
            await using var fx = new TestDbFixture();
            await fx.InitializeAndSeedAsync();
            await using var sp = fx.CreateServiceProvider();

            var auth = sp.GetRequiredService<IAuthService>();
            var shift = sp.GetRequiredService<IShiftService>();
            var settings = sp.GetRequiredService<AppSettings>();

            var rolePages = new (string Username, string[] Keys)[]
            {
                ("admin", ["dashboard", "pos", "orders", "menu", "warehouse", "vouchers", "users", "reports"]),
                ("staff", ["dashboard", "pos", "orders"]),
                ("thukho", ["warehouse"])
            };

            foreach (var (username, keys) in rolePages)
            {
                step($"MainForm role={username}");
                var session = sp.GetRequiredService<AppSession>();
                session.Clear();
                var login = await auth.LoginAsync(username, "123456", DateTime.Today.AddHours(10));
                Assert.True(login.Success);
                session.SetUser(login.User!);

                if (login.User!.Role != UserRole.WarehouseStaff)
                {
                    var started = await shift.StartShiftAsync(login.User!.Id, 400000m);
                    session.SetActiveSession(started.Data);
                }

                using var main = new MainForm(sp, session, settings, shift);
                ConfigureOffScreenForm(main);
                main.WindowState = FormWindowState.Normal;
                main.Size = new Size(1366, 768);
                main.Show();
                await StaTestRunner.YieldUiAsync(80);

                foreach (var key in keys)
                {
                    step($"MainForm {username} -> {key}");
                    await main.NavigateAsync(key);
                    await StaTestRunner.YieldUiAsync(25);
                }

                main.Size = new Size(1200, 720);
                await StaTestRunner.YieldUiAsync(25);
                main.Close();

                if (login.User!.Role != UserRole.WarehouseStaff)
                    await shift.EndShiftAsync(login.User!.Id, 400000m, "Test done");
            }
        }, timeoutSeconds: 45);
    }

    [Fact]
    public void AllDialogsAndForms_ConstructShowInteractAndDisposeSafely()
    {
        StaTestRunner.RunAsync(async step =>
        {
            step("init fixture");
            await using var fx = new TestDbFixture();
            await fx.InitializeAndSeedAsync();
            await using var sp = fx.CreateServiceProvider();

            var menuSvc = sp.GetRequiredService<IMenuService>();
            var invSvc = sp.GetRequiredService<IInventoryService>();
            var shiftSvc = sp.GetRequiredService<IShiftService>();
            var voucherSvc = sp.GetRequiredService<IVoucherService>();
            var userSvc = sp.GetRequiredService<IUserScheduleService>();
            var settings = sp.GetRequiredService<AppSettings>();

            var categories = await menuSvc.GetCategoriesAsync();
            var products = await menuSvc.GetProductsAsync();
            var ingredients = await invSvc.GetIngredientsAsync();
            var users = await userSvc.GetUsersAsync();

            // LoginForm
            step("LoginForm");
            using (var dlg = new LoginForm(sp.GetRequiredService<IAuthService>(), sp.GetRequiredService<AppSession>(), settings))
            {
                ConfigureOffScreenForm(dlg);
                dlg.Show();
                await StaTestRunner.YieldUiAsync();
                dlg.Close();
            }

            // StartShiftDialog (null & non-null other session) + default 0 starting cash
            step("StartShiftDialog");
            using (var dlg = new StartShiftDialog(shiftSvc, users[0].Id, null))
            {
                ConfigureOffScreenForm(dlg);
                dlg.Show();
                Assert.Equal(0m, FindControls<NumericUpDown>(dlg).Single().Value);
                await StaTestRunner.YieldUiAsync();
                dlg.Close();
            }
            var dummySession = new WorkSessionDto(1, users[0].Id, "Admin", DateTime.Now.AddHours(-3), null, 300000m, 0m, 0m, 0m, 0m, 300000m, 0m, null, WorkSessionStatus.Open);
            using (var dlg = new StartShiftDialog(shiftSvc, users[0].Id, dummySession))
            {
                ConfigureOffScreenForm(dlg);
                dlg.Show();
                Assert.Equal(0m, FindControls<NumericUpDown>(dlg).Single().Value);
                await StaTestRunner.YieldUiAsync();
                dlg.Close();
            }

            // WithdrawCashDialog
            step("WithdrawCashDialog");
            using (var dlg = new WithdrawCashDialog("Đang rút trong Ca #1 (Admin)", 300000m))
            {
                ConfigureOffScreenForm(dlg);
                dlg.Show();
                var num = FindControls<NumericUpDown>(dlg).Single();
                Assert.Equal(0m, num.Value);
                num.Value = 50000m;
                FindControls<TextBox>(dlg).Single(tb => tb.Multiline).Text = "Chi mua đá viên";
                Assert.Equal(50000m, dlg.Amount);
                Assert.Equal("Chi mua đá viên", dlg.Reason);
                await StaTestRunner.YieldUiAsync();
                dlg.Close();
            }

            // EndShiftDialog (normal + negative ExpectedCash + extreme ExpectedCash clamping)
            step("EndShiftDialog");
            foreach (var expected in new[] { 550000m, -1000m, 2_000_000_000m })
            {
                var summary = new EndShiftSummaryDto(1, DateTime.Now.AddHours(-2), 500000m, 50000m, 25000m, 75000m, expected, 2);
                using var dlg = new EndShiftDialog(shiftSvc, users[0].Id, summary);
                ConfigureOffScreenForm(dlg);
                dlg.Show();
                var num = FindControls<NumericUpDown>(dlg).Single();
                num.Value = Math.Clamp(expected + 10000m, num.Minimum, num.Maximum);
                num.Value = Math.Clamp(expected - 10000m, num.Minimum, num.Maximum);
                await StaTestRunner.YieldUiAsync();
                dlg.Close();
            }

            // PaymentDialog (normal, zero, huge + toggle Cash/Transfer)
            foreach (var amt in new[] { 85000m, 0m, 2_000_000_000m })
            {
                step($"PaymentDialog amt={amt}");
                PaymentMethod? lastChanged = null;
                using var dlg = new PaymentDialog(amt, settings, m => lastChanged = m);
                ConfigureOffScreenForm(dlg);
                dlg.Show();
                var radios = FindControls<RadioButton>(dlg);
                var rbTransfer = radios.Single(r => r.Text.Contains("Chuyển khoản"));
                var rbCash = radios.Single(r => r.Text.Contains("Tiền mặt"));
                rbTransfer.Checked = true;
                Assert.Equal(PaymentMethod.Transfer, lastChanged);
                rbCash.Checked = true;
                Assert.Equal(PaymentMethod.Cash, lastChanged);
                await StaTestRunner.YieldUiAsync();
                dlg.Close();
            }

            // ReceiptDialog & OrderDetailDialog (Paid & Cancelled)
            step("Receipt & OrderDetail");
            var sampleItems = new List<OrderItemDto> { new(1, "Latte Đá", 2, 45000m, 8900m, "Ít đá") };
            var paidOrder = new OrderDto(1, "ORD-1", 1, "Staff", 1, DateTime.Now, 90000m, "WELCOME", 10m, 9000m, 81000m, PaymentMethod.Cash, OrderStatus.Paid, null, null, null, sampleItems);
            var cancelledOrder = new OrderDto(2, "ORD-2", 1, "Staff", 1, DateTime.Now, 90000m, null, 0m, 0m, 90000m, PaymentMethod.Transfer, OrderStatus.Cancelled, "Admin", DateTime.Now, "Đổi món", sampleItems);
            foreach (var o in new[] { paidOrder, cancelledOrder })
            {
                using var receipt = new ReceiptDialog(o, settings);
                ConfigureOffScreenForm(receipt);
                receipt.Show();
                Assert.Contains(FindControls<Button>(receipt), b => b.Text == "In hóa đơn");
                Assert.Contains(FindControls<Button>(receipt), b => b.Text == "In tem cốc");
                using var detail = new OrderDetailDialog(o, settings);
                ConfigureOffScreenForm(detail);
                detail.Show();
                Assert.Contains(FindControls<Button>(detail), b => b.Text == "In hóa đơn");
                await StaTestRunner.YieldUiAsync();
                receipt.Close();
                detail.Close();
            }

            // ProductEditorDialog (new, existing, empty ingredients & extreme price clamping)
            step("ProductEditorDialog");
            using (var dlgNew = new ProductEditorDialog(menuSvc, categories, ingredients, null))
            {
                ConfigureOffScreenForm(dlgNew);
                dlgNew.Show();
                FindControls<Button>(dlgNew).Single(b => b.Text == "+ Thêm").PerformClick();
                FindControls<Button>(dlgNew).Single(b => b.Text == "Xóa nguyên liệu đã chọn").PerformClick();
                await StaTestRunner.YieldUiAsync();
                dlgNew.Close();
            }
            var extremeProduct = products.First() with { Price = 2_000_000_000m };
            using (var dlgEdit = new ProductEditorDialog(menuSvc, categories, Array.Empty<IngredientDto>(), extremeProduct))
            {
                ConfigureOffScreenForm(dlgEdit);
                dlgEdit.Show();
                await StaTestRunner.YieldUiAsync();
                dlgEdit.Close();
            }

            // Warehouse & Admin Dialogs
            step("AddIngredientDialog");
            using (var addIng = new AddIngredientDialog(invSvc, users[0].Id))
            {
                ConfigureOffScreenForm(addIng);
                addIng.Show();
                await StaTestRunner.YieldUiAsync();
                addIng.Close();
            }
            step("Import & ExportStockDialog");
            foreach (var ingList in new IReadOnlyList<IngredientDto>[] { ingredients, Array.Empty<IngredientDto>() })
            {
                using var imp = new ImportStockDialog(invSvc, users[0].Id, ingList, ingList.FirstOrDefault()?.Id);
                ConfigureOffScreenForm(imp);
                imp.Show();
                using var exp = new ExportStockDialog(invSvc, users[0].Id, ingList, ingList.FirstOrDefault()?.Id);
                ConfigureOffScreenForm(exp);
                exp.Show();
                await StaTestRunner.YieldUiAsync();
                imp.Close();
                exp.Close();
            }
            step("AddVoucherDialog");
            using (var addVoucher = new AddVoucherDialog(voucherSvc))
            {
                ConfigureOffScreenForm(addVoucher);
                addVoucher.Show();
                var chk = FindControls<CheckBox>(addVoucher).Single();
                chk.Checked = true;
                chk.Checked = false;
                await StaTestRunner.YieldUiAsync();
                addVoucher.Close();
            }
            step("UserEditorDialog");
            using (var userNew = new UserEditorDialog(userSvc, null))
            using (var userEdit = new UserEditorDialog(userSvc, users.First()))
            {
                ConfigureOffScreenForm(userNew);
                ConfigureOffScreenForm(userEdit);
                userNew.Show();
                userEdit.Show();
                await StaTestRunner.YieldUiAsync();
                userNew.Close();
                userEdit.Close();
            }
        }, timeoutSeconds: 60);
    }

    [Fact]
    public async Task Helpers_ImageHelperAndFormatHelper_HandleAllBranches()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"corrupt_{Guid.NewGuid():N}.jpg");
        string? copiedRelPath = null;
        try
        {
            File.WriteAllText(tempFile, "not a valid image");
            await ImageHelper.PreloadProductImagesAsync(
            [
                ("Assets/Images/drink/latte-da.jpg", "Latte"),
                ("Assets/Images/drink/tra-dao-cam-sa.jpg", "Trà Đào Cam Sả"),
                (null, "Missing"),
                (tempFile, "Corrupt")
            ], 160);
            await ImageHelper.PreloadLogoAsync(64);

            using (var fallback1 = ImageHelper.LoadProductImage(null, "Cà Phê", 120))
            using (var fallback2 = ImageHelper.LoadProductImage(tempFile, "Trà", 120))
            using (var validImg = ImageHelper.LoadProductImage("Assets/Images/drink/latte-da.jpg", "Latte", 160))
            using (var logo = ImageHelper.LoadLogo(64))
            {
                Assert.NotNull(fallback1);
                Assert.NotNull(fallback2);
                Assert.NotNull(validImg);
                Assert.NotNull(logo);
            }

            // Disposing returned bitmaps must not corrupt cached images
            using var validImgAgain = ImageHelper.LoadProductImage("Assets/Images/drink/latte-da.jpg", "Latte", 160);
            using var fallbackAgain = ImageHelper.LoadProductImage(null, "Cà Phê", 120);
            using var logoAgain = ImageHelper.LoadLogo(64);
            Assert.True(validImgAgain.Width > 0 && validImgAgain.Height > 0);
            Assert.True(fallbackAgain.Width > 0 && fallbackAgain.Height > 0);
            Assert.NotNull(logoAgain);
            Assert.True(logoAgain!.Width > 0 && logoAgain.Height > 0);

            copiedRelPath = ImageHelper.CopyImageToAssets("Assets/Images/drink/latte-da.jpg", 1);
            Assert.NotNull(copiedRelPath);
            var fullCopied = Path.Combine(AppContext.BaseDirectory, copiedRelPath!.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(fullCopied));
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
            if (!string.IsNullOrEmpty(copiedRelPath))
            {
                var fullCopied = Path.Combine(AppContext.BaseDirectory, copiedRelPath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(fullCopied)) File.Delete(fullCopied);
            }
        }

        Assert.Contains("đ", FormatHelper.Currency(25000m));
        foreach (var r in Enum.GetValues<UserRole>()) Assert.False(string.IsNullOrEmpty(FormatHelper.RoleLabel(r)));
        foreach (var s in Enum.GetValues<ShiftType>()) Assert.False(string.IsNullOrEmpty(FormatHelper.ShiftLabel(s)));
        foreach (var p in Enum.GetValues<PaymentMethod>()) Assert.False(string.IsNullOrEmpty(FormatHelper.PaymentLabel(p)));
        foreach (var o in Enum.GetValues<OrderStatus>()) Assert.False(string.IsNullOrEmpty(FormatHelper.OrderStatusLabel(o)));
        foreach (var t in Enum.GetValues<InventoryTransactionType>()) Assert.False(string.IsNullOrEmpty(FormatHelper.InventoryTxLabel(t)));
    }

    [Fact]
    public void DataGridViewHeaderTheme_HasReadableSelectionColors()
    {
        using var grid = new DataGridView();
        CoffeeTheme.StyleDataGridView(grid);

        Assert.Equal(CoffeeTheme.Espresso, grid.ColumnHeadersDefaultCellStyle.BackColor);
        Assert.Equal(Color.White, grid.ColumnHeadersDefaultCellStyle.ForeColor);
        Assert.Equal(CoffeeTheme.Espresso, grid.ColumnHeadersDefaultCellStyle.SelectionBackColor);
        Assert.Equal(Color.White, grid.ColumnHeadersDefaultCellStyle.SelectionForeColor);
    }

    [Fact]
    public void ReceiptPrintHelper_BuildsAndRendersReceiptAndStickersWithoutPrinter()
    {
        var settings = new AppSettings();
        var items = new List<OrderItemDto>
        {
            new(1, "Trà Đào Cam Sả", 2, 39000m, 10000m, "Ít ngọt"),
            new(2, "Bánh Mì Bơ Tỏi", 1, 29000m, 9000m, null)
        };
        var order = new OrderDto(
            1, "ORD-PRINT-01", 1, "Thu Ngân A", 1, DateTime.Now,
            107000m, "WELCOME", 10m, 10700m, 96300m,
            PaymentMethod.Transfer, OrderStatus.Paid, null, null, null, items);

        using var receiptDoc = ReceiptPrintHelper.BuildReceiptDocument(order, settings);
        var receiptCtrl = new System.Drawing.Printing.PreviewPrintController();
        receiptDoc.PrintController = receiptCtrl;
        receiptDoc.Print();
        var receiptPages = receiptCtrl.GetPreviewPageInfo();
        Assert.True(receiptPages.Length >= 1);

        using var stickerDoc = ReceiptPrintHelper.BuildStickerDocument(order);
        var stickerCtrl = new System.Drawing.Printing.PreviewPrintController();
        stickerDoc.PrintController = stickerCtrl;
        stickerDoc.Print();
        var stickerPages = stickerCtrl.GetPreviewPageInfo();
        Assert.Equal(3, stickerPages.Length); // 2 + 1 = 3 cup stickers
    }
}
