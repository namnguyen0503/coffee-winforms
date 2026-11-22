using CoffeeShop.Application.Common;
using CoffeeShop.Application.DTOs;
using CoffeeShop.Application.Interfaces;
using CoffeeShop.Domain.Enums;
using CoffeeShop.WinForms.Forms;
using CoffeeShop.WinForms.Helpers;
using CoffeeShop.WinForms.Theme;

namespace CoffeeShop.WinForms.Controls;

public class PosControl : UserControl
{
    private readonly IMenuService _menuService;
    private readonly IOrderService _orderService;
    private readonly IVoucherService _voucherService;
    private readonly IShiftService _shiftService;
    private readonly AppSession _appSession;
    private readonly AppSettings _settings;
    private readonly Action _onShiftChanged;
    private readonly Func<CustomerDisplayForm?>? _getCustomerDisplay;
    private readonly Action? _onToggleCustomerDisplay;

    private readonly FlowLayoutPanel _categoryBar = new();
    private readonly TextBox _txtSearch = new();
    private readonly FlowLayoutPanel _productGrid = new();
    private readonly FlowLayoutPanel _cartListPanel = new();
    private readonly TextBox _txtVoucher = new();
    private readonly Label _lblVoucherStatus = new();
    private readonly Label _lblSubtotal = new();
    private readonly Label _lblDiscount = new();
    private readonly Label _lblFinal = new();
    private readonly Button _btnCheckout;

    private List<CategoryDto> _categories = new();
    private List<ProductDto> _products = new();
    private int? _selectedCategoryId;
    private readonly List<(int? CategoryId, Button Button)> _categoryButtons = new();
    private string? _lastCategoriesSignature;
    private readonly List<(ProductDto Product, Panel Card)> _productCardItems = new();
    private string? _lastProductsSignature;

    private sealed class CartLine
    {
        public required ProductDto Product { get; init; }
        public int Quantity { get; set; }
        public string? Note { get; set; }
    }

    private readonly List<CartLine> _cart = new();
    private string? _appliedVoucherCode;
    private decimal _appliedDiscountPercent;
    private CustomerDisplayForm? _localCustomerDisplay;

    public PosControl(
        IMenuService menuService,
        IOrderService orderService,
        IVoucherService voucherService,
        IShiftService shiftService,
        AppSession appSession,
        AppSettings settings,
        Action onShiftChanged,
        Func<CustomerDisplayForm?>? getCustomerDisplay = null,
        Action? onToggleCustomerDisplay = null)
    {
        _menuService = menuService;
        _orderService = orderService;
        _voucherService = voucherService;
        _shiftService = shiftService;
        _appSession = appSession;
        _settings = settings;
        _onShiftChanged = onShiftChanged;
        _getCustomerDisplay = getCustomerDisplay;
        _onToggleCustomerDisplay = onToggleCustomerDisplay;

        Dock = DockStyle.Fill;
        BackColor = CoffeeTheme.Cream;
        Padding = new Padding(12);
        CoffeeTheme.EnableDoubleBuffering(this);
        CoffeeTheme.EnableDoubleBuffering(_categoryBar);
        CoffeeTheme.EnableDoubleBuffering(_productGrid);
        CoffeeTheme.EnableDoubleBuffering(_cartListPanel);

        var split = CoffeeTheme.CreateSafeSplitContainer(0.62, 360, 360);

        // LEFT: Categories + Search + Product Cards
        var leftPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 8, 0) };

        var filterBar = new Panel { Dock = DockStyle.Top, Height = 86 };
        _categoryBar.Dock = DockStyle.Top;
        _categoryBar.Height = 44;
        _categoryBar.WrapContents = false;
        _categoryBar.AutoScroll = true;

        var searchRow = new Panel { Dock = DockStyle.Bottom, Height = 36 };
        _txtSearch.Dock = DockStyle.Fill;
        _txtSearch.PlaceholderText = "Tìm nhanh tên món...";
        _txtSearch.Font = new Font("Segoe UI", 10.5F);
        _txtSearch.TextChanged += (_, _) => ApplyProductFilter();

        var btnCustScreen = CoffeeTheme.CreateSecondaryButton("Màn hình khách", 136, 32);
        btnCustScreen.Dock = DockStyle.Right;
        btnCustScreen.Click += (_, _) => ToggleCustomerDisplay();

        searchRow.Controls.Add(_txtSearch);
        searchRow.Controls.Add(btnCustScreen);
        filterBar.Controls.Add(searchRow);
        filterBar.Controls.Add(_categoryBar);

        _productGrid.Dock = DockStyle.Fill;
        _productGrid.AutoScroll = true;
        _productGrid.Padding = new Padding(2);

        leftPanel.Controls.Add(_productGrid);
        leftPanel.Controls.Add(filterBar);
        split.Panel1.Controls.Add(leftPanel);

        // RIGHT: Inline Cart + Voucher + Totals + Checkout
        var rightPanel = CoffeeTheme.CreateCardPanel(new Padding(12));
        rightPanel.Dock = DockStyle.Fill;

        var cartHeaderRow = new Panel { Dock = DockStyle.Top, Height = 36 };
        var lblCartTitle = new Label
        {
            Text = "GIỎ HÀNG HIỆN TẠI",
            Font = CoffeeTheme.SectionFont,
            ForeColor = CoffeeTheme.Espresso,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        var btnClearAll = CoffeeTheme.CreateSecondaryButton("Xóa hết", 84, 28);
        btnClearAll.Dock = DockStyle.Right;
        btnClearAll.Click += (_, _) => ClearCart();
        cartHeaderRow.Controls.Add(lblCartTitle);
        cartHeaderRow.Controls.Add(btnClearAll);

        _cartListPanel.Dock = DockStyle.Fill;
        _cartListPanel.FlowDirection = FlowDirection.TopDown;
        _cartListPanel.WrapContents = false;
        _cartListPanel.AutoScroll = true;
        _cartListPanel.BackColor = CoffeeTheme.Cream;
        _cartListPanel.Padding = new Padding(6);
        _cartListPanel.SizeChanged += (_, _) => ResizeCartRowWidths();

        var footer = new Panel { Dock = DockStyle.Bottom, Height = 216, Padding = new Padding(0, 8, 0, 0) };

        var voucherRow = new Panel { Dock = DockStyle.Top, Height = 34 };
        _txtVoucher.Dock = DockStyle.Fill;
        _txtVoucher.CharacterCasing = CharacterCasing.Upper;
        _txtVoucher.PlaceholderText = "Mã giảm giá (VD: WELCOME, VIP20...)";
        var btnApplyVoucher = CoffeeTheme.CreatePrimaryButton("Áp dụng", 90, 30);
        btnApplyVoucher.Dock = DockStyle.Right;
        btnApplyVoucher.Click += async (_, _) => await ApplyVoucherAsync();
        voucherRow.Controls.Add(_txtVoucher);
        voucherRow.Controls.Add(btnApplyVoucher);

        _lblVoucherStatus.Dock = DockStyle.Top;
        _lblVoucherStatus.Height = 22;
        _lblVoucherStatus.Font = CoffeeTheme.SmallFont;
        _lblVoucherStatus.ForeColor = CoffeeTheme.AccentGreen;

        _lblSubtotal.Dock = DockStyle.Top;
        _lblSubtotal.Height = 24;
        _lblSubtotal.Font = CoffeeTheme.BodyFont;

        _lblDiscount.Dock = DockStyle.Top;
        _lblDiscount.Height = 24;
        _lblDiscount.Font = CoffeeTheme.BodyBoldFont;
        _lblDiscount.ForeColor = CoffeeTheme.AccentGreen;

        _lblFinal.Dock = DockStyle.Top;
        _lblFinal.Height = 36;
        _lblFinal.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
        _lblFinal.ForeColor = CoffeeTheme.DangerRed;

        _btnCheckout = CoffeeTheme.CreateSuccessButton("THANH TOÁN ĐƠN", 300, 44);
        _btnCheckout.Dock = DockStyle.Bottom;
        _btnCheckout.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        _btnCheckout.Click += async (_, _) => await CheckoutAsync();

        footer.Controls.Add(_btnCheckout);
        footer.Controls.Add(_lblFinal);
        footer.Controls.Add(_lblDiscount);
        footer.Controls.Add(_lblSubtotal);
        footer.Controls.Add(_lblVoucherStatus);
        footer.Controls.Add(voucherRow);

        rightPanel.Controls.Add(_cartListPanel);
        rightPanel.Controls.Add(footer);
        rightPanel.Controls.Add(cartHeaderRow);
        split.Panel2.Controls.Add(rightPanel);

        Controls.Add(split);
        RefreshCartView();
    }

    public async Task LoadAsync(bool promptStartShift = true)
    {
        var user = _appSession.CurrentUser;
        if (user is null || IsDisposed) return;

        var activeSession = await _shiftService.GetActiveSessionAsync(user.Id);
        if (IsDisposed) return;

        _appSession.SetActiveSession(activeSession);
        _onShiftChanged();

        if (activeSession is null && promptStartShift)
        {
            var anyOpen = await _shiftService.GetAnyActiveSessionAsync();
            if (IsDisposed) return;

            using var dlg = new StartShiftDialog(_shiftService, user.Id, anyOpen);
            if (dlg.ShowDialog(FindForm()) == DialogResult.OK && dlg.StartedSession is not null)
            {
                _appSession.SetActiveSession(dlg.StartedSession);
                _onShiftChanged();
            }
        }

        var catTask = _menuService.GetCategoriesAsync();
        var prodTask = _menuService.GetProductsAsync(includeUnavailable: false);
        await Task.WhenAll(catTask, prodTask);
        if (IsDisposed) return;

        _categories = (await catTask).ToList();
        _products = (await prodTask).ToList();

        await ImageHelper.PreloadProductImagesAsync(
            _products.Select(p => (p.ImagePath, p.Name)),
            160);
        if (IsDisposed) return;

        RenderCategories();
        RenderProducts();
    }

    private void RenderCategories()
    {
        var sig = string.Join("|", _categories.Select(c => $"{c.Id}:{c.Name}:{c.SortOrder}"));
        if (_lastCategoriesSignature == sig && _categoryButtons.Count > 0)
        {
            UpdateCategoryButtonStyles();
            return;
        }

        _lastCategoriesSignature = sig;
        CoffeeTheme.SuspendDrawing(_categoryBar);
        _categoryBar.SuspendLayout();
        try
        {
            foreach (Control old in _categoryBar.Controls)
                old.Dispose();
            _categoryBar.Controls.Clear();
            _categoryButtons.Clear();

            var btnAll = CreateCategoryButton("Tất cả", null);
            _categoryButtons.Add((null, btnAll));

            foreach (var cat in _categories)
                _categoryButtons.Add((cat.Id, CreateCategoryButton(cat.Name, cat.Id)));

            _categoryBar.Controls.AddRange(_categoryButtons.Select(x => (Control)x.Button).ToArray());
            UpdateCategoryButtonStyles();
        }
        finally
        {
            _categoryBar.ResumeLayout();
            CoffeeTheme.ResumeDrawing(_categoryBar);
        }
    }

    private Button CreateCategoryButton(string text, int? categoryId)
    {
        var btn = CoffeeTheme.CreateSecondaryButton(text, 115, 34);
        btn.Margin = new Padding(0, 2, 8, 2);
        btn.Click += (_, _) =>
        {
            _selectedCategoryId = categoryId;
            UpdateCategoryButtonStyles();
            ApplyProductFilter();
        };
        return btn;
    }

    private void UpdateCategoryButtonStyles()
    {
        foreach (var (categoryId, btn) in _categoryButtons)
        {
            var isSelected = _selectedCategoryId == categoryId;
            if (isSelected)
            {
                btn.BackColor = CoffeeTheme.Mocha;
                btn.ForeColor = Color.White;
                btn.FlatAppearance.BorderSize = 0;
                btn.FlatAppearance.MouseOverBackColor = CoffeeTheme.MochaHover;
            }
            else
            {
                btn.BackColor = Color.White;
                btn.ForeColor = CoffeeTheme.TextPrimary;
                btn.FlatAppearance.BorderSize = 1;
                btn.FlatAppearance.BorderColor = CoffeeTheme.Border;
                btn.FlatAppearance.MouseOverBackColor = CoffeeTheme.Cream;
            }
        }
    }

    private void RenderProducts(bool forceRebuild = false)
    {
        var sig = string.Join("|", _products.Select(p =>
            $"{p.Id}:{p.Name}:{p.Price}:{p.CategoryId}:{p.ImagePath}:{p.MaxProducibleQuantity}"));

        if (!forceRebuild && _lastProductsSignature == sig && _productCardItems.Count == _products.Count)
        {
            ApplyProductFilter();
            return;
        }

        _lastProductsSignature = sig;
        var builtCards = new List<(ProductDto Product, Panel Card)>(_products.Count);
        foreach (var p in _products)
            builtCards.Add((p, CreateProductCard(p)));

        CoffeeTheme.SuspendDrawing(_productGrid);
        _productGrid.SuspendLayout();
        try
        {
            foreach (Control old in _productGrid.Controls)
            {
                foreach (Control child in old.Controls)
                {
                    if (child is PictureBox pb && pb.Image is not null)
                    {
                        var img = pb.Image;
                        pb.Image = null;
                        img.Dispose();
                    }
                }
                old.Dispose();
            }
            _productGrid.Controls.Clear();
            _productCardItems.Clear();

            _productCardItems.AddRange(builtCards);
            _productGrid.Controls.AddRange(_productCardItems.Select(x => (Control)x.Card).ToArray());
            ApplyProductFilterCore();
        }
        finally
        {
            _productGrid.ResumeLayout();
            CoffeeTheme.ResumeDrawing(_productGrid);
        }
    }

    private void ApplyProductFilter()
    {
        if (IsDisposed)
            return;

        CoffeeTheme.SuspendDrawing(_productGrid);
        _productGrid.SuspendLayout();
        try
        {
            ApplyProductFilterCore();
        }
        finally
        {
            _productGrid.ResumeLayout();
            CoffeeTheme.ResumeDrawing(_productGrid);
        }
    }

    private void ApplyProductFilterCore()
    {
        var term = _txtSearch.Text.Trim();
        foreach (var (product, card) in _productCardItems)
        {
            var matches =
                (!_selectedCategoryId.HasValue || product.CategoryId == _selectedCategoryId.Value) &&
                (string.IsNullOrEmpty(term) || product.Name.Contains(term, StringComparison.OrdinalIgnoreCase));

            if (card.Visible != matches)
                card.Visible = matches;
        }
    }

    private Panel CreateProductCard(ProductDto p)
    {
        var outOfStock = p.MaxProducibleQuantity.HasValue && p.MaxProducibleQuantity.Value <= 0;
        var card = new Panel
        {
            Size = new Size(164, 212),
            BackColor = outOfStock ? ColorTranslator.FromHtml("#F5F5F5") : CoffeeTheme.Surface,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(6),
            Cursor = outOfStock ? Cursors.No : Cursors.Hand
        };

        var pic = new PictureBox
        {
            Dock = DockStyle.Top,
            Height = 110,
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = ImageHelper.LoadProductImage(p.ImagePath, p.Name, 160),
            BackColor = CoffeeTheme.Cream
        };

        var lblName = new Label
        {
            Text = p.Name,
            Font = CoffeeTheme.BodyBoldFont,
            ForeColor = CoffeeTheme.TextPrimary,
            Location = new Point(6, 114),
            Size = new Size(150, 36),
            TextAlign = ContentAlignment.MiddleCenter
        };

        var lblPrice = new Label
        {
            Text = FormatHelper.Currency(p.Price),
            Font = CoffeeTheme.BodyBoldFont,
            ForeColor = CoffeeTheme.DangerRed,
            Location = new Point(6, 150),
            Size = new Size(150, 22),
            TextAlign = ContentAlignment.MiddleCenter
        };

        var stockText = p.MaxProducibleQuantity.HasValue
            ? (outOfStock ? "Hết nguyên liệu" : $"Có thể bán: {p.MaxProducibleQuantity}")
            : "Sẵn sàng";

        var lblStock = new Label
        {
            Text = stockText,
            Font = CoffeeTheme.SmallFont,
            ForeColor = outOfStock ? CoffeeTheme.DangerRed : CoffeeTheme.AccentGreen,
            Location = new Point(6, 174),
            Size = new Size(150, 24),
            TextAlign = ContentAlignment.MiddleCenter
        };

        void OnAddClick(object? s, EventArgs e)
        {
            if (outOfStock)
            {
                MessageBox.Show($"Món '{p.Name}' hiện không đủ nguyên liệu để pha chế.", "Hết nguyên liệu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            AddToCart(p);
        }

        card.Click += OnAddClick;
        pic.Click += OnAddClick;
        lblName.Click += OnAddClick;
        lblPrice.Click += OnAddClick;
        lblStock.Click += OnAddClick;

        card.Controls.AddRange(new Control[] { lblStock, lblPrice, lblName, pic });
        return card;
    }

    private void AddToCart(ProductDto product)
    {
        var existing = _cart.FirstOrDefault(c => c.Product.Id == product.Id && string.IsNullOrEmpty(c.Note));
        var currentTotalOfProduct = _cart.Where(c => c.Product.Id == product.Id).Sum(c => c.Quantity);
        if (product.MaxProducibleQuantity.HasValue && currentTotalOfProduct + 1 > product.MaxProducibleQuantity.Value)
        {
            MessageBox.Show($"Không đủ nguyên liệu cho món '{product.Name}' (tối đa {product.MaxProducibleQuantity}).", "Giới hạn tồn kho", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (existing is not null)
            existing.Quantity++;
        else
            _cart.Add(new CartLine { Product = product, Quantity = 1 });

        RefreshCartView();
    }

    private void ClearCart()
    {
        _cart.Clear();
        _appliedVoucherCode = null;
        _appliedDiscountPercent = 0m;
        _txtVoucher.Clear();
        _lblVoucherStatus.Text = string.Empty;
        RefreshCartView();
    }

    private (decimal Subtotal, decimal Discount, decimal FinalAmount) CalculateTotals()
    {
        var subtotal = _cart.Sum(c => c.Product.Price * c.Quantity);
        var discount = Math.Round(subtotal * (_appliedDiscountPercent / 100m), 2, MidpointRounding.AwayFromZero);
        var final = Math.Max(0m, subtotal - discount);
        return (subtotal, discount, final);
    }

    private void RefreshCartView()
    {
        var rowWidth = Math.Max(260, _cartListPanel.ClientSize.Width - 18);
        var builtRows = new List<Control>(Math.Max(1, _cart.Count));

        if (_cart.Count == 0)
        {
            builtRows.Add(new Label
            {
                Text = "\n\nChưa có món nào trong giỏ.\nBấm vào món bên trái để thêm.",
                Font = CoffeeTheme.BodyFont,
                ForeColor = CoffeeTheme.TextMuted,
                Size = new Size(rowWidth, 110),
                TextAlign = ContentAlignment.MiddleCenter
            });
        }
        else
        {
            foreach (var line in _cart)
                builtRows.Add(CreateCartRowCard(line, rowWidth));
        }

        CoffeeTheme.SuspendDrawing(_cartListPanel);
        _cartListPanel.SuspendLayout();
        try
        {
            foreach (Control old in _cartListPanel.Controls)
                old.Dispose();
            _cartListPanel.Controls.Clear();
            _cartListPanel.Controls.AddRange(builtRows.ToArray());
        }
        finally
        {
            _cartListPanel.ResumeLayout();
            CoffeeTheme.ResumeDrawing(_cartListPanel);
        }

        UpdateTotalsAndDisplayOnly();
    }

    private Panel CreateCartRowCard(CartLine line, int rowWidth)
    {
        var card = new Panel
        {
            Size = new Size(rowWidth, 82),
            BackColor = CoffeeTheme.Surface,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 0, 0, 6),
            Padding = new Padding(8, 6, 8, 6),
            Tag = "cart-row"
        };

        var topRow = new Panel
        {
            Dock = DockStyle.Top,
            Height = 38
        };

        var controlsFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 148,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 4, 0, 0)
        };

        var btnMinus = CoffeeTheme.CreateSecondaryButton("-", 28, 28);
        btnMinus.Tag = "minus";
        btnMinus.Margin = new Padding(0, 0, 3, 0);

        var txtQty = new TextBox
        {
            Text = line.Quantity.ToString(),
            Size = new Size(44, 28),
            TextAlign = HorizontalAlignment.Center,
            Font = CoffeeTheme.BodyBoldFont,
            Margin = new Padding(0, 1, 3, 0),
            Tag = "qty"
        };

        var btnPlus = CoffeeTheme.CreateSecondaryButton("+", 28, 28);
        btnPlus.Tag = "plus";
        btnPlus.Margin = new Padding(0, 0, 4, 0);

        var btnRemove = CoffeeTheme.CreateDangerButton("×", 28, 28);
        btnRemove.Tag = "remove";
        btnRemove.Margin = new Padding(0);

        controlsFlow.Controls.AddRange(new Control[] { btnMinus, txtQty, btnPlus, btnRemove });

        var infoPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 0, 6, 0)
        };

        var lblName = new Label
        {
            Text = line.Product.Name,
            Font = CoffeeTheme.BodyBoldFont,
            ForeColor = CoffeeTheme.TextPrimary,
            Dock = DockStyle.Top,
            Height = 20,
            AutoEllipsis = true
        };

        var lblLineTotal = new Label
        {
            Text = FormatHelper.Currency(line.Product.Price * line.Quantity),
            Font = CoffeeTheme.SmallFont,
            ForeColor = CoffeeTheme.DangerRed,
            Dock = DockStyle.Fill
        };

        infoPanel.Controls.Add(lblLineTotal);
        infoPanel.Controls.Add(lblName);

        topRow.Controls.Add(infoPanel);
        topRow.Controls.Add(controlsFlow);

        var bottomRow = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 4, 0, 0)
        };

        var txtNote = new TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "Ghi chú (ít đá, ít đường, mang về...)",
            Text = line.Note ?? string.Empty,
            Font = CoffeeTheme.SmallFont,
            ForeColor = CoffeeTheme.AccentBlue,
            Tag = "note"
        };
        bottomRow.Controls.Add(txtNote);

        bool IsOverStock(int targetQty)
        {
            if (!line.Product.MaxProducibleQuantity.HasValue)
                return false;
            var otherQty = _cart.Where(c => !ReferenceEquals(c, line) && c.Product.Id == line.Product.Id).Sum(c => c.Quantity);
            return otherQty + targetQty > line.Product.MaxProducibleQuantity.Value;
        }

        void UpdateStockVisual(int qty)
        {
            txtQty.BackColor = IsOverStock(qty)
                ? ColorTranslator.FromHtml("#FFEBEE")
                : Color.White;
        }
        UpdateStockVisual(line.Quantity);

        var syncingText = false;
        btnMinus.Click += (_, _) =>
        {
            if (line.Quantity <= 1)
                _cart.Remove(line);
            else
                line.Quantity--;
            RefreshCartView();
        };

        btnPlus.Click += (_, _) =>
        {
            var next = line.Quantity + 1;
            if (IsOverStock(next))
            {
                MessageBox.Show(
                    $"Số lượng vượt quá nguyên liệu tồn kho (tối đa {line.Product.MaxProducibleQuantity}).",
                    "Giới hạn tồn kho",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }
            line.Quantity = next;
            RefreshCartView();
        };

        btnRemove.Click += (_, _) =>
        {
            _cart.Remove(line);
            RefreshCartView();
        };

        txtQty.TextChanged += (_, _) =>
        {
            if (syncingText)
                return;

            if (int.TryParse(txtQty.Text.Trim(), out var parsed) && parsed >= 1)
            {
                line.Quantity = parsed;
                lblLineTotal.Text = FormatHelper.Currency(line.Product.Price * parsed);
                UpdateStockVisual(parsed);
                UpdateTotalsAndDisplayOnly();
            }
        };

        txtQty.Leave += (_, _) =>
        {
            if (!int.TryParse(txtQty.Text.Trim(), out var parsed) || parsed < 1)
            {
                syncingText = true;
                try
                {
                    txtQty.Text = line.Quantity.ToString();
                }
                finally
                {
                    syncingText = false;
                }
                lblLineTotal.Text = FormatHelper.Currency(line.Product.Price * line.Quantity);
                UpdateStockVisual(line.Quantity);
                UpdateTotalsAndDisplayOnly();
            }
        };

        txtNote.TextChanged += (_, _) =>
        {
            line.Note = string.IsNullOrWhiteSpace(txtNote.Text) ? null : txtNote.Text;
            SyncCustomerDisplay();
        };

        card.Controls.Add(bottomRow);
        card.Controls.Add(topRow);
        return card;
    }

    private void ResizeCartRowWidths()
    {
        if (_cartListPanel.IsDisposed || _cartListPanel.Controls.Count == 0)
            return;

        var rowWidth = Math.Max(260, _cartListPanel.ClientSize.Width - 18);
        _cartListPanel.SuspendLayout();
        foreach (Control c in _cartListPanel.Controls)
        {
            if (c.Width != rowWidth)
                c.Width = rowWidth;
        }
        _cartListPanel.ResumeLayout();
    }

    private void UpdateTotalsAndDisplayOnly()
    {
        var (subtotal, discount, final) = CalculateTotals();
        _lblSubtotal.Text = $"Tạm tính: {FormatHelper.Currency(subtotal)}";
        _lblDiscount.Text = _appliedDiscountPercent > 0
            ? $"Giảm giá ({_appliedVoucherCode} - {_appliedDiscountPercent:0.##}%): -{FormatHelper.Currency(discount)}"
            : "Giảm giá: 0 đ";
        _lblFinal.Text = $"TỔNG CỘNG: {FormatHelper.Currency(final)}";

        _btnCheckout.Enabled = _cart.Count > 0;
        SyncCustomerDisplay();
    }

    private async Task ApplyVoucherAsync()
    {
        var user = _appSession.CurrentUser;
        if (user is null) return;

        var code = _txtVoucher.Text.Trim();
        if (string.IsNullOrEmpty(code))
        {
            _appliedVoucherCode = null;
            _appliedDiscountPercent = 0m;
            _lblVoucherStatus.Text = string.Empty;
            UpdateTotalsAndDisplayOnly();
            return;
        }

        var (subtotal, _, _) = CalculateTotals();
        var check = await _voucherService.ValidateAsync(code, subtotal, user.Role);
        if (IsDisposed) return;

        if (!check.IsValid)
        {
            _lblVoucherStatus.ForeColor = CoffeeTheme.DangerRed;
            _lblVoucherStatus.Text = check.Message;
            _appliedVoucherCode = null;
            _appliedDiscountPercent = 0m;
            UpdateTotalsAndDisplayOnly();
            return;
        }

        var pct = check.DiscountPercent;
        if (check.RequiresAdminCustomPercent)
        {
            var custom = PromptHelper.ShowPercentInput(FindForm(), "ADMINVIP", "Nhập phần trăm giảm giá (0 - 100%):", 100m);
            if (!custom.HasValue) return;
            pct = custom.Value;
        }

        _appliedVoucherCode = check.Code;
        _appliedDiscountPercent = pct;
        _lblVoucherStatus.ForeColor = CoffeeTheme.AccentGreen;
        _lblVoucherStatus.Text = $"Đã áp dụng mã {check.Code} (-{pct:0.##}%)";
        UpdateTotalsAndDisplayOnly();
    }

    private async Task CheckoutAsync()
    {
        var user = _appSession.CurrentUser;
        if (user is null || _cart.Count == 0) return;

        if (_appSession.ActiveSession is null)
        {
            var anyOpen = await _shiftService.GetAnyActiveSessionAsync();
            if (IsDisposed) return;

            using var shiftDlg = new StartShiftDialog(_shiftService, user.Id, anyOpen);
            if (shiftDlg.ShowDialog(FindForm()) != DialogResult.OK || shiftDlg.StartedSession is null)
                return;

            _appSession.SetActiveSession(shiftDlg.StartedSession);
            _onShiftChanged();
        }

        var (_, _, finalAmount) = CalculateTotals();
        using var payDlg = new PaymentDialog(finalAmount, _settings, method =>
        {
            var cust = GetActiveCustomerDisplay();
            if (cust is { IsDisposed: false })
            {
                if (method == PaymentMethod.Transfer)
                    cust.ShowTransferPayment(finalAmount);
                else
                    cust.ShowWelcome();
            }
        });

        if (payDlg.ShowDialog(FindForm()) != DialogResult.OK)
        {
            var cust = GetActiveCustomerDisplay();
            if (cust is { IsDisposed: false })
                cust.ShowWelcome();
            return;
        }

        _btnCheckout.Enabled = false;
        var req = new CheckoutRequest(
            user.Id,
            payDlg.SelectedMethod,
            _appliedVoucherCode,
            _appliedVoucherCode == "ADMINVIP" ? _appliedDiscountPercent : null,
            _cart.Select(c => new CartItemRequest(c.Product.Id, c.Quantity, c.Note)).ToList());

        var res = await _orderService.CheckoutAsync(req);
        if (IsDisposed) return;

        if (!res.Success || res.Data is null)
        {
            MessageBox.Show(res.Message, "Thanh toán thất bại", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _btnCheckout.Enabled = _cart.Count > 0;
            return;
        }

        var activeCust = GetActiveCustomerDisplay();
        if (activeCust is { IsDisposed: false })
            activeCust.ShowPaymentSuccess();

        using var receipt = new ReceiptDialog(res.Data, _settings);
        receipt.ShowDialog(FindForm());

        ClearCart();

        _products = (await _menuService.GetProductsAsync(includeUnavailable: false)).ToList();
        if (IsDisposed) return;

        await ImageHelper.PreloadProductImagesAsync(
            _products.Select(p => (p.ImagePath, p.Name)),
            160);
        if (IsDisposed) return;

        RenderProducts();
    }

    private CustomerDisplayForm? GetActiveCustomerDisplay()
    {
        var shared = _getCustomerDisplay?.Invoke();
        if (shared is { IsDisposed: false })
            return shared;

        return _localCustomerDisplay is { IsDisposed: false } ? _localCustomerDisplay : null;
    }

    private void ToggleCustomerDisplay()
    {
        if (_onToggleCustomerDisplay is not null)
        {
            _onToggleCustomerDisplay();
            SyncCustomerDisplay();
            return;
        }

        if (_localCustomerDisplay is { IsDisposed: false })
        {
            _localCustomerDisplay.Focus();
            return;
        }

        _localCustomerDisplay = new CustomerDisplayForm(_settings);
        _localCustomerDisplay.PositionOnSecondaryScreenIfAvailable();
        _localCustomerDisplay.Show();
        SyncCustomerDisplay();
    }

    internal void SyncCustomerDisplay()
    {
        var cust = GetActiveCustomerDisplay();
        if (cust is null || cust.IsDisposed)
            return;

        var (subtotal, discount, final) = CalculateTotals();
        var items = _cart
            .Select(c => new CustomerDisplayItem(c.Product.Name, c.Quantity, c.Product.Price, c.Note, c.Product.ImagePath))
            .ToList();
        cust.UpdateCart(items, subtotal, discount, final);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_localCustomerDisplay is { IsDisposed: false })
            {
                _localCustomerDisplay.Close();
                _localCustomerDisplay.Dispose();
            }

            foreach (Control c in _productGrid.Controls)
            {
                foreach (Control child in c.Controls)
                {
                    if (child is PictureBox pb && pb.Image is not null)
                    {
                        var img = pb.Image;
                        pb.Image = null;
                        img.Dispose();
                    }
                }
            }
        }
        base.Dispose(disposing);
    }
}
