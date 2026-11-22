# Nguyễn Văn Coffee — WinForms + SQL Server (.NET 8)

Phiên bản tái cấu trúc hoàn toàn từ mã nguồn PHP/MySQL cũ (`php_version(old)/`) sang kiến trúc hiện đại **Clean Layered Architecture (.NET 8 WinForms + Entity Framework Core + SQL Server)**.

## Cải tiến kiến trúc & nghiệp vụ so với bản PHP cũ
1. **Phân tách 4 tầng rõ ràng (`Domain`, `Application`, `Infrastructure`, `WinForms`) + Dependency Injection**: Không còn trộn SQL/HTML trong giao diện. Mỗi service dùng `IDbContextFactory<CoffeeDbContext>` ngắn hạn tránh xung đột tracking trên desktop.
2. **Sửa lỗi tài chính & tồn kho nghiêm trọng**:
   - Sử dụng `decimal(18,2)` cho tiền tệ và `decimal(18,3)` cho định lượng (thay thế `FLOAT` / `INT`).
   - Lưu snapshot `UnitPrice` và `UnitCost` (giá vốn theo công thức tại thời điểm bán) trên từng `OrderItem` để báo cáo lợi nhuận quá khứ luôn chính xác kể cả khi đổi giá menu hoặc giá nhập nguyên liệu.
   - Tách rõ `InventoryTransactionType.Import` (cập nhật giá vốn bình quân gia quyền WAC) với `CancelRestock` (hoàn kho khi hủy đơn mà không làm sai lệch WAC).
   - Giao dịch thanh toán & kiểm tra tồn kho nguyên liệu nằm trong **Database Transaction nguyên tử**, ngăn chặn tồn kho âm.
3. **Quản lý ca làm việc (Work Session) chính xác**:
   - Tính doanh thu theo `WorkSessionId`, tách riêng **Tiền mặt (`CashSales`)** và **Chuyển khoản (`TransferSales`)**.
   - Tự động tính **Tiền kỳ vọng trong két (`ExpectedCash = StartCash + CashSales`)**, **Chênh lệch kiểm kê (`CashVariance`)**, và **Lệch bàn giao giữa các ca (`HandoverDiff`)**.
   - Hỗ trợ tự động chốt ca cũ nếu nhân viên trước quên chốt ca.
4. **Bảo mật & phân quyền thực thụ**:
   - Truy vấn EF Core tham số hóa 100% (loại bỏ SQL Injection).
   - Mật khẩu băm bằng `BCrypt.Net-Next`.
   - Kiểm tra lịch phân ca động từ `WorkSchedules` cho vai trò `Staff` (không còn bug ghi đè `users.status_work` toàn cục).
   - Kiểm tra quyền `Admin` ngay trong tầng Service cho các tác vụ nhạy cảm (`ADMINVIP`, hủy đơn, xuất kho thủ công).

---

## Cấu trúc Solution
- `php_version(old)/` — Mã nguồn PHP/MySQL tham chiếu ban đầu.
- `src/CoffeeShop.Domain/` — Thực thể nghiệp vụ và Enum (11 bảng).
- `src/CoffeeShop.Application/` — DTO, `AppSession`, `AppSettings`, và Service Interfaces.
- `src/CoffeeShop.Infrastructure/` — `CoffeeDbContext`, Fluent API Configurations, `DbSeeder`, và Service Implementations.
- `src/CoffeeShop.WinForms/` — Giao diện WinForms Desktop (Dashboard, POS, Màn hình Khách, Đơn hàng, Thực đơn & Công thức, Kho nguyên liệu, Voucher, Nhân sự & Phân ca, Báo cáo & Đối soát dòng tiền).
- `tests/CoffeeShop.Tests/` — Bộ kiểm thử tích hợp xUnit (SQLite In-Memory + xác minh T-SQL DDL cho SQL Server).
- `database/CoffeeShopDb_Schema.sql` — Script T-SQL tạo cơ sở dữ liệu SQL Server được sinh từ EF Core model.

---

## Hướng dẫn cài đặt & chạy ứng dụng

### 1. Khởi động SQL Server
Đảm bảo dịch vụ **SQL Server (`MSSQLSERVER`)** đang chạy trên máy, hoặc chỉnh lại chuỗi kết nối trong `src/CoffeeShop.WinForms/appsettings.json`:
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=.;Database=CoffeeShopDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
}
```
Khi chạy ứng dụng lần đầu, hệ thống tự động gọi `EnsureCreatedAsync()` để tạo database `CoffeeShopDb` và seed dữ liệu mẫu (có thể chạy thủ công `database/CoffeeShopDb_Schema.sql` trong SSMS nếu muốn tạo schema trước).

### 2. Chạy ứng dụng WinForms
```powershell
dotnet run --project src/CoffeeShop.WinForms/CoffeeShop.WinForms.csproj
```

### 3. Tài khoản mặc định (tất cả dùng mật khẩu `123456`)
| Vai trò | Tên đăng nhập | Mật khẩu | Ghi chú |
|---|---|---|---|
| Quản lý (Admin) | `admin` | `123456` | Toàn quyền + mã `ADMINVIP` + hủy đơn |
| Thu ngân (Staff) | `staff` | `123456` | POS, lịch sử cá nhân (được seed lịch 7 ngày) |
| Thủ kho | `thukho` | `123456` | Nhập/xuất kho, cảnh báo tồn, lịch sử kho |

### 4. Chạy bộ kiểm thử tự động
```powershell
dotnet test CoffeeShop.sln -c Release --verbosity normal
```
