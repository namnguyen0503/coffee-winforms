using CoffeeShop.Application.DTOs;
using CoffeeShop.Application.Interfaces;
using CoffeeShop.WinForms.Forms;
using CoffeeShop.WinForms.Helpers;
using CoffeeShop.WinForms.Theme;

namespace CoffeeShop.WinForms.Controls;

public class MenuManagementControl : UserControl
{
    private readonly IMenuService _menuService;
    private readonly IInventoryService _inventoryService;

    private readonly ComboBox _cboCategory = new();
    private readonly TextBox _txtSearch = new();
    private readonly DataGridView _grid = new();

    private List<CategoryDto> _categories = new();
    private List<ProductDto> _products = new();
    private bool _isLoading;

    public MenuManagementControl(IMenuService menuService, IInventoryService inventoryService)
    {
        _menuService = menuService;
        _inventoryService = inventoryService;

        Dock = DockStyle.Fill;
        BackColor = CoffeeTheme.Cream;
        Padding = new Padding(16);

        var topPanel = CoffeeTheme.CreateCardPanel(new Padding(12));
        topPanel.Dock = DockStyle.Top;
        topPanel.Height = 62;

        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        _cboCategory.Width = 170;
        _cboCategory.DropDownStyle = ComboBoxStyle.DropDownList;
        _cboCategory.SelectedIndexChanged += async (_, _) =>
        {
            if (_isLoading) return;
            await RefreshProductsAsync();
        };

        _txtSearch.Width = 220;
        _txtSearch.PlaceholderText = "Tìm tên món...";
        _txtSearch.TextChanged += async (_, _) =>
        {
            if (_isLoading) return;
            await RefreshProductsAsync();
        };

        var btnAdd = CoffeeTheme.CreateSuccessButton("+ Thêm món mới", 140, 32);
        btnAdd.Click += async (_, _) => await OpenEditorAsync(null);

        var btnEdit = CoffeeTheme.CreatePrimaryButton("Sửa món & Công thức", 165, 32);
        btnEdit.Click += async (_, _) =>
        {
            var p = GetSelectedProduct();
            if (p is not null) await OpenEditorAsync(p);
        };

        var btnDelete = CoffeeTheme.CreateDangerButton("Xóa món", 100, 32);
        btnDelete.Click += async (_, _) => await DeleteSelectedAsync();

        flow.Controls.AddRange(new Control[] { _cboCategory, _txtSearch, btnAdd, btnEdit, btnDelete });
        topPanel.Controls.Add(flow);

        var gridCard = CoffeeTheme.CreateCardPanel();
        gridCard.Dock = DockStyle.Fill;
        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        CoffeeTheme.StyleDataGridView(_grid);
        _grid.CellDoubleClick += async (_, _) =>
        {
            var p = GetSelectedProduct();
            if (p is not null) await OpenEditorAsync(p);
        };

        _grid.Columns.Add("Id", "ID");
        _grid.Columns.Add("Name", "Tên món");
        _grid.Columns.Add("Category", "Danh mục");
        _grid.Columns.Add("Price", "Giá bán");
        _grid.Columns.Add("Cost", "Giá vốn");
        _grid.Columns.Add("Margin", "Lãi gộp");
        _grid.Columns.Add("Recipe", "Công thức");
        _grid.Columns.Add("Status", "Trạng thái");
        _grid.Columns["Id"]!.FillWeight = 35;

        gridCard.Controls.Add(_grid);
        Controls.Add(gridCard);
        Controls.Add(topPanel);
    }

    public async Task LoadAsync()
    {
        if (IsDisposed) return;

        _isLoading = true;
        try
        {
            _categories = (await _menuService.GetCategoriesAsync()).ToList();
            if (IsDisposed) return;

            var filterItems = new List<CategoryDto> { new(0, "Tất cả danh mục", 0) };
            filterItems.AddRange(_categories);
            _cboCategory.DisplayMember = nameof(CategoryDto.Name);
            _cboCategory.ValueMember = nameof(CategoryDto.Id);
            _cboCategory.DataSource = filterItems;
        }
        finally
        {
            _isLoading = false;
        }

        await RefreshProductsAsync();
    }

    private async Task RefreshProductsAsync()
    {
        if (IsDisposed) return;

        int? catId = _cboCategory.SelectedItem is CategoryDto cat && cat.Id > 0
            ? cat.Id
            : (_cboCategory.SelectedValue is int id && id > 0 ? id : null);
        _products = (await _menuService.GetProductsAsync(catId, _txtSearch.Text, includeUnavailable: true)).ToList();
        if (IsDisposed) return;

        _grid.Rows.Clear();
        foreach (var p in _products)
        {
            var profit = p.Price - p.EstimatedUnitCost;
            var marginPct = p.Price > 0 ? Math.Round((profit / p.Price) * 100m, 1) : 0m;
            var recipeSummary = p.Recipes.Count == 0
                ? "Chưa gán định lượng"
                : string.Join(", ", p.Recipes.Select(r => $"{r.IngredientName}: {r.QuantityRequired:0.##}{r.Unit}"));

            var idx = _grid.Rows.Add(
                p.Id,
                p.Name,
                p.CategoryName,
                FormatHelper.Currency(p.Price),
                FormatHelper.Currency(p.EstimatedUnitCost),
                $"{FormatHelper.Currency(profit)} ({marginPct:0.#}%)",
                recipeSummary,
                p.IsAvailable ? "Đang bán" : "Ngừng bán");

            if (!p.IsAvailable)
                _grid.Rows[idx].DefaultCellStyle.ForeColor = CoffeeTheme.TextMuted;
        }
    }

    private ProductDto? GetSelectedProduct()
    {
        if (_grid.CurrentRow is null) return null;
        var idx = _grid.CurrentRow.Index;
        return idx >= 0 && idx < _products.Count ? _products[idx] : null;
    }

    private async Task OpenEditorAsync(ProductDto? existing)
    {
        var ingredients = await _inventoryService.GetIngredientsAsync();
        using var dlg = new ProductEditorDialog(_menuService, _categories, ingredients, existing);
        if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
            await RefreshProductsAsync();
    }

    private async Task DeleteSelectedAsync()
    {
        var product = GetSelectedProduct();
        if (product is null) return;

        if (MessageBox.Show($"Xác nhận xóa món '{product.Name}' khỏi thực đơn?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        var res = await _menuService.SoftDeleteProductAsync(product.Id);
        if (!res.Success)
            MessageBox.Show(res.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);

        await RefreshProductsAsync();
    }
}
