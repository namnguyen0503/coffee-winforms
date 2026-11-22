using CoffeeShop.Application.DTOs;
using CoffeeShop.Application.Interfaces;
using CoffeeShop.WinForms.Helpers;
using CoffeeShop.WinForms.Theme;

namespace CoffeeShop.WinForms.Forms;

public class ProductEditorDialog : Form
{
    private readonly IMenuService _menuService;
    private readonly int? _productId;
    private readonly IReadOnlyList<IngredientDto> _ingredients;

    private readonly TextBox _txtName = new();
    private readonly NumericUpDown _numPrice = new();
    private readonly ComboBox _cboCategory = new();
    private readonly CheckBox _chkAvailable = new();
    private readonly PictureBox _picPreview = new();
    private readonly DataGridView _recipeGrid = new();
    private readonly ComboBox _cboIngredient = new();
    private readonly NumericUpDown _numReqQty = new();
    private readonly Label _lblCostPreview = new();
    private readonly Button _btnSave;

    private string? _selectedImagePath;
    private readonly List<(int IngredientId, string Name, string Unit, decimal Qty, decimal UnitCost)> _recipes = new();

    public ProductEditorDialog(
        IMenuService menuService,
        IReadOnlyList<CategoryDto> categories,
        IReadOnlyList<IngredientDto> ingredients,
        ProductDto? existing)
    {
        _menuService = menuService;
        _productId = existing?.Id;
        _ingredients = ingredients;
        _selectedImagePath = existing?.ImagePath;

        Text = existing is null ? "Thêm món mới" : $"Chỉnh sửa món: {existing.Name}";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(720, 560);
        BackColor = CoffeeTheme.Surface;
        Font = CoffeeTheme.BodyFont;

        // Left: Product info & image
        var lblName = new Label { Text = "Tên món *", Font = CoffeeTheme.BodyBoldFont, Location = new Point(20, 16), Size = new Size(280, 20) };
        _txtName.Location = new Point(20, 38);
        _txtName.Size = new Size(280, 28);
        _txtName.Text = existing?.Name ?? string.Empty;

        var lblPrice = new Label { Text = "Giá bán (VNĐ) *", Font = CoffeeTheme.BodyBoldFont, Location = new Point(20, 76), Size = new Size(280, 20) };
        _numPrice.Location = new Point(20, 98);
        _numPrice.Size = new Size(280, 28);
        _numPrice.Maximum = 100_000_000m;
        _numPrice.Increment = 1000m;
        _numPrice.ThousandsSeparator = true;
        _numPrice.Value = Math.Clamp(existing?.Price ?? 30000m, _numPrice.Minimum, _numPrice.Maximum);

        var lblCat = new Label { Text = "Danh mục *", Font = CoffeeTheme.BodyBoldFont, Location = new Point(20, 136), Size = new Size(280, 20) };
        _cboCategory.Location = new Point(20, 158);
        _cboCategory.Size = new Size(280, 28);
        _cboCategory.DropDownStyle = ComboBoxStyle.DropDownList;
        _cboCategory.DisplayMember = nameof(CategoryDto.Name);
        _cboCategory.ValueMember = nameof(CategoryDto.Id);
        _cboCategory.DataSource = categories.ToList();
        if (existing is not null && categories.Any(c => c.Id == existing.CategoryId))
            _cboCategory.SelectedValue = existing.CategoryId;

        _chkAvailable.Text = "Đang kinh doanh (Hiển thị trên POS)";
        _chkAvailable.Location = new Point(20, 198);
        _chkAvailable.Size = new Size(280, 24);
        _chkAvailable.Checked = existing?.IsAvailable ?? true;

        _picPreview.Location = new Point(20, 232);
        _picPreview.Size = new Size(280, 190);
        _picPreview.SizeMode = PictureBoxSizeMode.Zoom;
        _picPreview.BorderStyle = BorderStyle.FixedSingle;
        _picPreview.Image = ImageHelper.LoadProductImage(_selectedImagePath, existing?.Name ?? "New", 280);

        var btnChooseImg = CoffeeTheme.CreateSecondaryButton("Chọn ảnh món (.jpg/.png)", 280, 34);
        btnChooseImg.Location = new Point(20, 430);
        btnChooseImg.Click += (_, _) => PickImage();

        // Right: Recipe Builder
        var lblRecipe = new Label
        {
            Text = "Công thức định lượng (Trừ kho tự động khi bán)",
            Font = CoffeeTheme.BodyBoldFont,
            Location = new Point(324, 16),
            Size = new Size(376, 20)
        };

        _cboIngredient.Location = new Point(324, 42);
        _cboIngredient.Size = new Size(200, 28);
        _cboIngredient.DropDownStyle = ComboBoxStyle.DropDownList;
        _cboIngredient.DisplayMember = "Display";
        _cboIngredient.ValueMember = "Id";
        _cboIngredient.DataSource = ingredients.Select(i => new { i.Id, Display = $"{i.Name} ({i.Unit})" }).ToList();

        _numReqQty.Location = new Point(530, 42);
        _numReqQty.Size = new Size(86, 28);
        _numReqQty.DecimalPlaces = 2;
        _numReqQty.Minimum = 0.01m;
        _numReqQty.Maximum = 100000m;
        _numReqQty.Value = 10m;

        var btnAddIng = CoffeeTheme.CreatePrimaryButton("+ Thêm", 78, 28);
        btnAddIng.Location = new Point(622, 42);
        btnAddIng.Click += (_, _) => AddIngredientLine();

        _recipeGrid.Location = new Point(324, 80);
        _recipeGrid.Size = new Size(376, 310);
        CoffeeTheme.StyleDataGridView(_recipeGrid);
        _recipeGrid.ReadOnly = true;
        _recipeGrid.Columns.Add("Name", "Nguyên liệu");
        _recipeGrid.Columns.Add("Qty", "Định lượng");
        _recipeGrid.Columns.Add("Cost", "Giá vốn");
        _recipeGrid.Columns["Qty"]!.FillWeight = 60;
        _recipeGrid.Columns["Cost"]!.FillWeight = 65;

        var btnRemoveIng = CoffeeTheme.CreateSecondaryButton("Xóa nguyên liệu đã chọn", 200, 32);
        btnRemoveIng.Location = new Point(324, 398);
        btnRemoveIng.Click += (_, _) => RemoveSelectedIngredientLine();

        _lblCostPreview.Location = new Point(324, 438);
        _lblCostPreview.Size = new Size(376, 26);
        _lblCostPreview.Font = CoffeeTheme.BodyBoldFont;
        _lblCostPreview.ForeColor = CoffeeTheme.AccentGreen;

        var btnCancel = CoffeeTheme.CreateSecondaryButton("Hủy", 120, 40);
        btnCancel.Location = new Point(448, 502);
        btnCancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        _btnSave = CoffeeTheme.CreateSuccessButton("Lưu sản phẩm", 132, 40);
        _btnSave.Location = new Point(576, 502);
        _btnSave.Click += async (_, _) => await SaveAsync();

        if (existing is not null)
        {
            foreach (var r in existing.Recipes)
                _recipes.Add((r.IngredientId, r.IngredientName, r.Unit, r.QuantityRequired, r.IngredientUnitCost));
        }

        RefreshRecipeGrid();

        Controls.AddRange(new Control[]
        {
            lblName, _txtName, lblPrice, _numPrice, lblCat, _cboCategory, _chkAvailable,
            _picPreview, btnChooseImg,
            lblRecipe, _cboIngredient, _numReqQty, btnAddIng, _recipeGrid, btnRemoveIng, _lblCostPreview,
            btnCancel, _btnSave
        });
    }

    private void PickImage()
    {
        using var ofd = new OpenFileDialog
        {
            Title = "Chọn hình ảnh sản phẩm",
            Filter = "Hình ảnh (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp"
        };
        if (ofd.ShowDialog(this) != DialogResult.OK)
            return;

        var catId = _cboCategory.SelectedValue is int id ? id : 1;
        _selectedImagePath = ImageHelper.CopyImageToAssets(ofd.FileName, catId);
        _picPreview.Image?.Dispose();
        _picPreview.Image = ImageHelper.LoadProductImage(_selectedImagePath, _txtName.Text, 280);
    }

    private void AddIngredientLine()
    {
        if (_cboIngredient.SelectedValue is not int ingId)
            return;

        var ing = _ingredients.FirstOrDefault(i => i.Id == ingId);
        if (ing is null)
            return;

        var qty = _numReqQty.Value;
        var idx = _recipes.FindIndex(r => r.IngredientId == ingId);
        if (idx >= 0)
            _recipes[idx] = (ing.Id, ing.Name, ing.Unit, _recipes[idx].Qty + qty, ing.AverageUnitCost);
        else
            _recipes.Add((ing.Id, ing.Name, ing.Unit, qty, ing.AverageUnitCost));

        RefreshRecipeGrid();
    }

    private void RemoveSelectedIngredientLine()
    {
        if (_recipeGrid.CurrentRow is null)
            return;

        var rowIdx = _recipeGrid.CurrentRow.Index;
        if (rowIdx >= 0 && rowIdx < _recipes.Count)
        {
            _recipes.RemoveAt(rowIdx);
            RefreshRecipeGrid();
        }
    }

    private void RefreshRecipeGrid()
    {
        _recipeGrid.Rows.Clear();
        decimal totalCost = 0m;
        foreach (var r in _recipes)
        {
            var cost = Math.Round(r.Qty * r.UnitCost, 2);
            totalCost += cost;
            _recipeGrid.Rows.Add(r.Name, $"{r.Qty:0.###} {r.Unit}", FormatHelper.Currency(cost));
        }

        _lblCostPreview.Text = $"Giá vốn ước tính / ly: {FormatHelper.Currency(totalCost)}";
    }

    private async Task SaveAsync()
    {
        if (_cboCategory.SelectedValue is not int categoryId)
        {
            MessageBox.Show("Vui lòng chọn danh mục.", "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _btnSave.Enabled = false;
        var req = new SaveProductRequest(
            _productId,
            _txtName.Text,
            _numPrice.Value,
            categoryId,
            _selectedImagePath,
            _chkAvailable.Checked,
            _recipes.Select(r => new RecipeLineRequest(r.IngredientId, r.Qty)).ToList());

        var res = await _menuService.SaveProductAsync(req);
        if (!res.Success)
        {
            MessageBox.Show(res.Message, "Lỗi lưu món", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _btnSave.Enabled = true;
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}
