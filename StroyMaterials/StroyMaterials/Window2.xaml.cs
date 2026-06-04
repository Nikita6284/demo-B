using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace StroyMaterials
{
    public partial class Window2 : Window
    {
        private string connectionString;
        private int productId;
        private string imageDirectory;
        private string selectedPhotoFileName;
        private string oldPhotoFileName;

        public Window2(int id, string connStr, string imgDir)
        {
            // Блокировка открытия нескольких окон
            foreach (Window window in Application.Current.Windows)
                if (window is Window2 && window != this)
                { MessageBox.Show("Окно редактирования товара уже открыто!", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning); DialogResult = false; Close(); return; }

            InitializeComponent();
            connectionString = connStr;
            productId = id;
            imageDirectory = imgDir;

            LoadComboBoxes();

            if (productId > 0)
            {
                LoadProduct();
                Title = "Редактирование товара";
            }
            else
            {
                GenerateArticle();
                Title = "Добавление товара";
            }
        }

        // Генерация случайного артикула для нового товара
        private void GenerateArticle()
        {
            Random rand = new Random();
            string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            string article = "";
            for (int i = 0; i < 6; i++) article += chars[rand.Next(chars.Length)];
            txtArticle.Text = article;
        }

        // Загрузка выпадающих списков
        private void LoadComboBoxes()
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlDataAdapter catAdapter = new SqlDataAdapter("SELECT CategoryID, CategoryName FROM Categories", conn);
                DataTable dtCat = new DataTable();
                catAdapter.Fill(dtCat);
                cmbCategory.ItemsSource = dtCat.DefaultView;
                cmbCategory.DisplayMemberPath = "CategoryName";
                cmbCategory.SelectedValuePath = "CategoryID";

                SqlDataAdapter manAdapter = new SqlDataAdapter("SELECT ManufacturerID, ManufacturerName FROM Manufacturers", conn);
                DataTable dtMan = new DataTable();
                manAdapter.Fill(dtMan);
                cmbManufacturer.ItemsSource = dtMan.DefaultView;
                cmbManufacturer.DisplayMemberPath = "ManufacturerName";
                cmbManufacturer.SelectedValuePath = "ManufacturerID";

                SqlDataAdapter supAdapter = new SqlDataAdapter("SELECT SupplierID, SupplierName FROM Suppliers", conn);
                DataTable dtSup = new DataTable();
                supAdapter.Fill(dtSup);
                cmbSupplier.ItemsSource = dtSup.DefaultView;
                cmbSupplier.DisplayMemberPath = "SupplierName";
                cmbSupplier.SelectedValuePath = "SupplierID";
            }
        }

        // Загрузка данных товара при редактировании
        private void LoadProduct()
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string query = "SELECT * FROM Products WHERE ProductID = @id";
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@id", productId);

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        txtArticle.Text = reader["Article"].ToString();
                        txtArticle.IsEnabled = false;
                        txtName.Text = reader["Name"].ToString();
                        txtPrice.Text = reader["Price"].ToString();

                        // ⚠️ ИСПРАВЛЕНИЕ: конвертируем decimal в int для скидки
                        decimal discountDecimal = Convert.ToDecimal(reader["Discount"]);
                        int discountInt = (int)discountDecimal;
                        txtDiscount.Text = discountInt.ToString();

                        txtQuantity.Text = reader["StockQuantity"].ToString();
                        txtUnit.Text = reader["Unit"].ToString();
                        txtDescription.Text = reader["Description"].ToString();

                        selectedPhotoFileName = reader["PhotoFileName"].ToString();
                        oldPhotoFileName = selectedPhotoFileName;
                        txtPhoto.Text = selectedPhotoFileName;

                        cmbCategory.SelectedValue = reader["CategoryID"];
                        cmbManufacturer.SelectedValue = reader["ManufacturerID"];
                        cmbSupplier.SelectedValue = reader["SupplierID"];

                        if (!string.IsNullOrEmpty(selectedPhotoFileName))
                        {
                            string photoPath = Path.Combine(imageDirectory, selectedPhotoFileName);
                            if (File.Exists(photoPath)) LoadPhotoPreview(photoPath);
                        }
                    }
                }
            }
        }

        // Выбор файла фото
        private void btnSelectPhoto_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openDialog = new OpenFileDialog();
            openDialog.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp";
            openDialog.Title = "Выберите фото товара (300x200)";

            if (openDialog.ShowDialog() == true)
            {
                string destFile = Path.Combine(imageDirectory, $"{txtArticle.Text}.jpg");
                try
                {
                    // Ограничение размера фото до 300x200
                    BitmapImage original = new BitmapImage();
                    original.BeginInit();
                    original.UriSource = new Uri(openDialog.FileName);
                    original.CacheOption = BitmapCacheOption.OnLoad;
                    original.EndInit();

                    int newWidth = 300;
                    int newHeight = 200;
                    RenderTargetBitmap resized = new RenderTargetBitmap(newWidth, newHeight, 96, 96, PixelFormats.Pbgra32);
                    DrawingVisual visual = new DrawingVisual();
                    using (DrawingContext ctx = visual.RenderOpen()) ctx.DrawImage(original, new Rect(0, 0, newWidth, newHeight));
                    resized.Render(visual);

                    JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(resized));
                    using (FileStream fs = new FileStream(destFile, FileMode.Create)) encoder.Save(fs);

                    // Удаление старого фото при замене
                    if (!string.IsNullOrEmpty(oldPhotoFileName) && File.Exists(Path.Combine(imageDirectory, oldPhotoFileName)))
                        File.Delete(Path.Combine(imageDirectory, oldPhotoFileName));

                    selectedPhotoFileName = $"{txtArticle.Text}.jpg";
                    txtPhoto.Text = selectedPhotoFileName;
                    LoadPhotoPreview(destFile);
                    MessageBox.Show("Фото добавлено (300x200)", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex) { MessageBox.Show("Ошибка: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); }
            }
        }

        private void LoadPhotoPreview(string path)
        {
            try
            {
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(path);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelHeight = 90;
                bitmap.EndInit();
                photoPreview.Source = bitmap;
                photoPreviewBorder.Visibility = Visibility.Visible;
            }
            catch { photoPreviewBorder.Visibility = Visibility.Collapsed; }
        }

        // Сохранение товара
        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            // Валидация полей
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Введите наименование", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(txtPrice.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal price) || price < 0)
            {
                MessageBox.Show("Введите корректную цену", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // ⚠️ ИСПРАВЛЕНИЕ: конвертируем скидку с учётом запятой и точки
            string discountText = txtDiscount.Text.Trim().Replace(',', '.');
            if (!decimal.TryParse(discountText, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal discountDecimal) || discountDecimal < 0 || discountDecimal > 100)
            {
                MessageBox.Show("Скидка должна быть числом от 0 до 100", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            int discount = (int)discountDecimal;

            if (!int.TryParse(txtQuantity.Text, out int quantity) || quantity < 0)
            {
                MessageBox.Show("Введите корректное количество", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    if (productId == 0)
                    {
                        // INSERT для нового товара
                        string query = @"INSERT INTO Products (Article, Name, Unit, Price, SupplierID, ManufacturerID,
                                        CategoryID, Discount, StockQuantity, Description, PhotoFileName)
                                        VALUES (@art, @name, @unit, @price, @sup, @man, @cat, @disc, @qty, @desc, @photo)";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@art", txtArticle.Text);
                            cmd.Parameters.AddWithValue("@name", txtName.Text);
                            cmd.Parameters.AddWithValue("@unit", txtUnit.Text);
                            cmd.Parameters.AddWithValue("@price", price);
                            cmd.Parameters.AddWithValue("@sup", cmbSupplier.SelectedValue);
                            cmd.Parameters.AddWithValue("@man", cmbManufacturer.SelectedValue);
                            cmd.Parameters.AddWithValue("@cat", cmbCategory.SelectedValue);
                            cmd.Parameters.AddWithValue("@disc", discount);
                            cmd.Parameters.AddWithValue("@qty", quantity);
                            cmd.Parameters.AddWithValue("@desc", txtDescription.Text);
                            cmd.Parameters.AddWithValue("@photo", selectedPhotoFileName ?? "");
                            cmd.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        // UPDATE для существующего товара
                        string query = @"UPDATE Products SET Name=@name, Unit=@unit, Price=@price,
                                        SupplierID=@sup, ManufacturerID=@man, CategoryID=@cat,
                                        Discount=@disc, StockQuantity=@qty, Description=@desc, PhotoFileName=@photo
                                        WHERE ProductID=@id";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@id", productId);
                            cmd.Parameters.AddWithValue("@name", txtName.Text);
                            cmd.Parameters.AddWithValue("@unit", txtUnit.Text);
                            cmd.Parameters.AddWithValue("@price", price);
                            cmd.Parameters.AddWithValue("@sup", cmbSupplier.SelectedValue);
                            cmd.Parameters.AddWithValue("@man", cmbManufacturer.SelectedValue);
                            cmd.Parameters.AddWithValue("@cat", cmbCategory.SelectedValue);
                            cmd.Parameters.AddWithValue("@disc", discount);
                            cmd.Parameters.AddWithValue("@qty", quantity);
                            cmd.Parameters.AddWithValue("@desc", txtDescription.Text);
                            cmd.Parameters.AddWithValue("@photo", selectedPhotoFileName ?? "");
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
                MessageBox.Show("Товар сохранен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex) { MessageBox.Show("Ошибка: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
    }
}