using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StroyMaterials
{
    
    public class Product
    {
        public int ProductID { get; set; }
        public string Article { get; set; }
        public string Name { get; set; }
        public string Unit { get; set; }
        public decimal Price { get; set; }
        public decimal Discount { get; set; }
        public int StockQuantity { get; set; }
        public string Description { get; set; }
        public string PhotoFileName { get; set; }
        public string Category { get; set; }
        public string Manufacturer { get; set; }
        public string Supplier { get; set; }
        public decimal FinalPrice { get; set; }          
        public ImageSource PhotoImage { get; set; }     
        public bool IsHighDiscount => Discount > 12;     
        public bool IsOutOfStock => StockQuantity == 0;  
    }

    public partial class Window1 : Window
    {
        private string connectionString = @"Server=ASUS\SQLEXPRESS;Database=StroyMaterials;Integrated Security=True;";
        private string imageDirectory;
        private bool showingProducts = true;
        private List<Product> allProducts;
        private List<Product> filteredProducts;
        private int selectedProductId = 0;

        public Window1()
        {
            InitializeComponent();

            // Определение путя к папке с изображениями
            string projectDirectory = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).Parent.Parent.FullName;
            imageDirectory = Path.Combine(projectDirectory, "img");

            LoadLogo();
            SetupUIByRole();
            LoadProducts();
        }

        // Загрузка логотипа
        private void LoadLogo()
        {
            try
            {
                string logoPath = Path.Combine(imageDirectory, "icon.png");
                if (!File.Exists(logoPath)) logoPath = Path.Combine(imageDirectory, "logo.png");
                if (File.Exists(logoPath))
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(logoPath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    imgLogo.Source = bitmap;
                }
            }
            catch { }
        }

        // Настройка видимости панелей в зависимости от роли пользователя
        private void SetupUIByRole()
        {
            txtUserName.Text = $"{MainWindow.CurrentUserName} ({MainWindow.CurrentUserRole})";

            switch (MainWindow.CurrentUserRole)
            {
                case "Администратор":
                    btnOrders.Visibility = Visibility.Visible;
                    pnlFilters.Visibility = Visibility.Visible;
                    pnlAdmin.Visibility = Visibility.Visible;
                    break;
                case "Менеджер":
                    btnOrders.Visibility = Visibility.Visible;
                    pnlFilters.Visibility = Visibility.Visible;
                    break;
            }
        }

        // Загрузка товаров из БД
        private void LoadProducts()
        {
            showingProducts = true;
            txtTitle.Text = "Каталог товаров";
            btnBack.Visibility = Visibility.Collapsed;
            allProducts = new List<Product>();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string query = @"
                    SELECT p.ProductID, p.Article, p.Name, p.Unit, p.Price, 
                           p.Discount, p.StockQuantity, p.Description, p.PhotoFileName,
                           c.CategoryName, m.ManufacturerName, s.SupplierName
                    FROM Products p
                    JOIN Categories c ON p.CategoryID = c.CategoryID
                    JOIN Manufacturers m ON p.ManufacturerID = m.ManufacturerID
                    JOIN Suppliers s ON p.SupplierID = s.SupplierID";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Product prod = new Product
                        {
                            ProductID = reader.GetInt32(0),
                            Article = reader.GetString(1),
                            Name = reader.GetString(2),
                            Unit = reader.GetString(3),
                            Price = reader.GetDecimal(4),
                            Discount = reader.GetDecimal(5),
                            StockQuantity = reader.GetInt32(6),
                            Description = reader.IsDBNull(7) ? "" : reader.GetString(7),
                            PhotoFileName = reader.IsDBNull(8) ? null : reader.GetString(8),
                            Category = reader.GetString(9),
                            Manufacturer = reader.GetString(10),
                            Supplier = reader.GetString(11)
                        };
                        prod.FinalPrice = prod.Price * (1 - prod.Discount / 100);
                        prod.PhotoImage = LoadImage(prod.PhotoFileName);
                        allProducts.Add(prod);
                    }
                }
            }

            LoadManufacturersFilter();
            ApplyFilters();
        }

        // Загрузка изображения из папки img
        private BitmapImage LoadImage(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return GetDefaultImage();
            string path = Path.Combine(imageDirectory, fileName);
            if (File.Exists(path))
            {
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(path);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = 150;
                bitmap.EndInit();
                return bitmap;
            }
            return GetDefaultImage();
        }

        // Заглушка при отсутствии изображения
        private BitmapImage GetDefaultImage()
        {
            string path = Path.Combine(imageDirectory, "picture.png");
            if (File.Exists(path))
            {
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(path);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = 150;
                bitmap.EndInit();
                return bitmap;
            }
            return null;
        }

        // Заполнение выпадающего списка производителей для фильтра
        private void LoadManufacturersFilter()
        {
            var manufacturers = allProducts.Select(p => p.Manufacturer).Distinct().OrderBy(m => m).ToList();
            cmbManufacturer.Items.Clear();
            cmbManufacturer.Items.Add("Все производители");
            foreach (var m in manufacturers) cmbManufacturer.Items.Add(m);
            cmbManufacturer.SelectedIndex = 0;
        }

        // Применение фильтров, поиска и сортировки
        private void ApplyFilters()
        {
            filteredProducts = allProducts.ToList();

            // ПОИСК по тексту
            string search = txtSearch.Text;
            if (!string.IsNullOrEmpty(search) && search != "Поиск...")
            {
                search = search.ToLower();
                filteredProducts = filteredProducts.Where(p =>
                    p.Name.ToLower().Contains(search) ||
                    p.Article.ToLower().Contains(search) ||
                    p.Description.ToLower().Contains(search) ||
                    p.Manufacturer.ToLower().Contains(search) ||
                    p.Supplier.ToLower().Contains(search)).ToList();
            }

            // ФИЛЬТР по производителю
            if (cmbManufacturer.SelectedItem != null && cmbManufacturer.SelectedItem.ToString() != "Все производители")
            {
                string selected = cmbManufacturer.SelectedItem.ToString();
                filteredProducts = filteredProducts.Where(p => p.Manufacturer == selected).ToList();
            }

            // СОРТИРОВКА
            if (cmbSort.SelectedItem != null)
            {
                string sort = ((ComboBoxItem)cmbSort.SelectedItem).Tag.ToString();
                switch (sort)
                {
                    case "Name": filteredProducts = filteredProducts.OrderBy(p => p.Name).ToList(); break;
                    case "Price ASC": filteredProducts = filteredProducts.OrderBy(p => p.FinalPrice).ToList(); break;
                    case "Price DESC": filteredProducts = filteredProducts.OrderByDescending(p => p.FinalPrice).ToList(); break;
                    case "Discount DESC": filteredProducts = filteredProducts.OrderByDescending(p => p.Discount).ToList(); break;
                    case "StockQuantity DESC": filteredProducts = filteredProducts.OrderByDescending(p => p.StockQuantity).ToList(); break;
                }
            }

            DisplayProducts();
        }

        // Отображение отфильтрованных товаров
        private void DisplayProducts()
        {
            pnlCards.Children.Clear();
            foreach (var product in filteredProducts) pnlCards.Children.Add(CreateProductCard(product));
            if (filteredProducts.Count == 0)
                pnlCards.Children.Add(new TextBlock { Text = "Товары не найдены", FontSize = 16, Foreground = Brushes.Gray, Margin = new Thickness(20) });
        }

        // Создание карточки товара 
        private Border CreateProductCard(Product product)
        {
            // Определяем цвет фона карточки по условиям
            Brush cardBg = Brushes.White;
            if (product.Discount > 12)                          
                cardBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F4A460"));
            else if (product.StockQuantity == 0)                  
                cardBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ADD8E6"));

            Border card = new Border
            {
                Width = 350,
                Margin = new Thickness(10),
                Background = cardBg,
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10),
                Tag = product.ProductID
            };

            // Обработчик клика для выделения карточки 
            card.MouseLeftButtonDown += (s, e) =>
            {
                selectedProductId = product.ProductID;
                if (MainWindow.CurrentUserRole == "Администратор")
                {
                    foreach (Border child in pnlCards.Children) if (child is Border brd) brd.BorderBrush = Brushes.Gray;
                    card.BorderBrush = Brushes.Blue;
                    card.BorderThickness = new Thickness(3);
                    btnEdit.IsEnabled = true;
                    btnDelete.IsEnabled = true;
                }
            };

            // Структура карточки
            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // ФОТО
            Border photoBorder = new Border { Width = 100, Height = 100, Margin = new Thickness(5), Background = Brushes.White, Child = new Image { Source = product.PhotoImage, Stretch = Stretch.Uniform, Width = 90, Height = 90 } };
            Grid.SetColumn(photoBorder, 0);
            grid.Children.Add(photoBorder);

            // ИНФОРМАЦИЯ
            StackPanel infoPanel = new StackPanel { Margin = new Thickness(10, 0, 10, 0) };
            Grid.SetColumn(infoPanel, 1);
            infoPanel.Children.Add(new TextBlock { Text = $"{product.Category} | {product.Name}", FontSize = 14, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap });
            infoPanel.Children.Add(new TextBlock { Text = $"Описание: {product.Description}", FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 0) });
            infoPanel.Children.Add(new TextBlock { Text = $"Производитель: {product.Manufacturer}", FontSize = 11, Margin = new Thickness(0, 2, 0, 0) });
            infoPanel.Children.Add(new TextBlock { Text = $"Поставщик: {product.Supplier}", FontSize = 11 });

            // ЦЕНА 
            StackPanel pricePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 0) };
            if (product.Discount > 0)
            {
                pricePanel.Children.Add(new TextBlock { Text = $"{product.Price:N0} ₽", FontSize = 12, Foreground = Brushes.Red, TextDecorations = TextDecorations.Strikethrough });
                pricePanel.Children.Add(new TextBlock { Text = $" {product.FinalPrice:N0} ₽", FontSize = 14, FontWeight = FontWeights.Bold, Foreground = Brushes.Black, Margin = new Thickness(5, 0, 0, 0) });
            }
            else pricePanel.Children.Add(new TextBlock { Text = $"{product.Price:N0} ₽", FontSize = 14, FontWeight = FontWeights.Bold });
            infoPanel.Children.Add(pricePanel);
            infoPanel.Children.Add(new TextBlock { Text = $"Ед. изм.: {product.Unit}", FontSize = 11 });
            infoPanel.Children.Add(new TextBlock { Text = $"Остаток: {product.StockQuantity} шт.", FontSize = 11, Foreground = product.StockQuantity > 0 ? Brushes.Green : Brushes.Red, FontWeight = FontWeights.Bold });
            grid.Children.Add(infoPanel);

            // БЛОК СО СКИДКОЙ
            Border discountBorder = new Border { Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF9C4")), CornerRadius = new CornerRadius(5), Padding = new Thickness(10), Margin = new Thickness(5), VerticalAlignment = VerticalAlignment.Top };
            discountBorder.Child = new TextBlock { Text = $"Скидка\n{product.Discount}%", FontSize = 14, FontWeight = FontWeights.Bold, Foreground = product.Discount > 12 ? Brushes.DarkRed : Brushes.Orange, TextAlignment = TextAlignment.Center };
            Grid.SetColumn(discountBorder, 2);
            grid.Children.Add(discountBorder);

            card.Child = grid;
            return card;
        }

        // Загрузка и отображение заказов
        private void LoadOrders()
        {
            showingProducts = false;
            txtTitle.Text = "Список заказов";
            btnBack.Visibility = Visibility.Visible;
            pnlCards.Children.Clear();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string query = @"
                    SELECT o.OrderID, o.OrderNumber, o.OrderDate, o.DeliveryDate, 
                           o.PickupCode, p.Address, u.FullName, s.StatusName
                    FROM Orders o
                    JOIN PickupPoints p ON o.PickupPointID = p.PointID
                    JOIN Users u ON o.UserID = u.UserID
                    JOIN OrderStatus s ON o.StatusID = s.StatusID
                    ORDER BY o.OrderDate DESC";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int orderId = reader.GetInt32(0);
                        int orderNumber = reader.GetInt32(1);
                        DateTime orderDate = reader.GetDateTime(2);
                        DateTime? deliveryDate = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3);
                        string pickupCode = reader.GetString(4);
                        string address = reader.GetString(5);
                        string clientName = reader.GetString(6);
                        string status = reader.GetString(7);
                        pnlCards.Children.Add(CreateOrderCard(orderId, orderNumber, orderDate, deliveryDate, pickupCode, address, clientName, status));
                    }
                }
            }
        }

        // Создание карточки заказа
        private Border CreateOrderCard(int orderId, int orderNumber, DateTime orderDate, DateTime? deliveryDate,
                                        string code, string address, string client, string status)
        {
            // Цвет статуса
            Brush statusColor = status == "Завершен" ? Brushes.Green : (status == "Новый" ? Brushes.Blue : Brushes.Orange);
            Border card = new Border
            {
                Width = 320,
                Margin = new Thickness(10),
                Background = Brushes.White,
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10),
                Tag = orderId
            };

            // Выделение карточки при клике
            card.MouseLeftButtonDown += (s, e) =>
            {
                if (MainWindow.CurrentUserRole == "Администратор")
                {
                    foreach (Border child in pnlCards.Children) if (child is Border brd) brd.BorderBrush = Brushes.Gray;
                    card.BorderBrush = Brushes.Blue;
                    card.BorderThickness = new Thickness(3);
                    btnEditOrder.IsEnabled = true;
                    btnDeleteOrder.IsEnabled = true;
                }
            };

            StackPanel panel = new StackPanel();
            panel.Children.Add(new Border { Background = statusColor, CornerRadius = new CornerRadius(5), Padding = new Thickness(5), Child = new TextBlock { Text = status, Foreground = Brushes.White, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center } });
            panel.Children.Add(new TextBlock { Text = $"Заказ №{orderNumber}", FontSize = 16, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 10, 0, 5), HorizontalAlignment = HorizontalAlignment.Center });
            panel.Children.Add(new TextBlock { Text = $"Клиент: {client}", FontSize = 12, Margin = new Thickness(0, 0, 0, 5) });
            panel.Children.Add(new TextBlock { Text = $"Дата заказа: {orderDate:dd.MM.yyyy}", FontSize = 12, Margin = new Thickness(0, 0, 0, 5) });
            panel.Children.Add(new TextBlock { Text = $"Дата доставки: {(deliveryDate?.ToString("dd.MM.yyyy") ?? "не указана")}", FontSize = 12, Margin = new Thickness(0, 0, 0, 5) });
            panel.Children.Add(new TextBlock { Text = $"Код получения: {code}", FontSize = 12, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 5) });
            panel.Children.Add(new TextBlock { Text = $"Пункт выдачи: {address}", FontSize = 11, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Gray });
            card.Child = panel;
            return card;
        }

        // Обработчики событий
        private void btnProducts_Click(object sender, RoutedEventArgs e) => LoadProducts();
        private void btnOrders_Click(object sender, RoutedEventArgs e) => LoadOrders();
        private void btnBack_Click(object sender, RoutedEventArgs e) => LoadProducts();
        private void btnExit_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Вы уверены, что хотите выйти?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            { new MainWindow().Show(); Close(); }
        }

        private void txtSearch_GotFocus(object sender, RoutedEventArgs e) { if (txtSearch.Text == "Поиск...") txtSearch.Text = ""; }
        private void txtSearch_LostFocus(object sender, RoutedEventArgs e) { if (string.IsNullOrWhiteSpace(txtSearch.Text)) txtSearch.Text = "Поиск..."; }
        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e) { if (showingProducts && txtSearch.Text != "Поиск...") ApplyFilters(); }
        private void cmbManufacturer_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (showingProducts && cmbManufacturer.IsLoaded) ApplyFilters(); }
        private void cmbSort_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (showingProducts && cmbSort.IsLoaded) ApplyFilters(); }
        private void btnReset_Click(object sender, RoutedEventArgs e) { txtSearch.Text = "Поиск..."; cmbManufacturer.SelectedIndex = 0; cmbSort.SelectedIndex = 0; ApplyFilters(); }

        // Добавление товара
        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            foreach (Window window in Application.Current.Windows)
                if (window is Window2) { MessageBox.Show("Окно редактирования уже открыто!", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            Window2 editWnd = new Window2(0, connectionString, imageDirectory);
            editWnd.Owner = this;
            if (editWnd.ShowDialog() == true) LoadProducts();
        }

        // Редактирование товара
        private void btnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (selectedProductId == 0) { MessageBox.Show("Выберите товар (нажмите на карточку)", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            foreach (Window window in Application.Current.Windows)
                if (window is Window2) { MessageBox.Show("Окно редактирования уже открыто!", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            Window2 editWnd = new Window2(selectedProductId, connectionString, imageDirectory);
            editWnd.Owner = this;
            if (editWnd.ShowDialog() == true) LoadProducts();
        }

        // Удаление товара с проверкой наличия в заказах
        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (selectedProductId == 0) { MessageBox.Show("Выберите товар (нажмите на карточку)", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            Product selectedProd = allProducts.FirstOrDefault(p => p.ProductID == selectedProductId);
            if (selectedProd == null) return;

            // Проверка на наличие товара в заказе
            bool isInOrder = false;
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM OrderProducts WHERE ProductID = @id", conn);
                cmd.Parameters.AddWithValue("@id", selectedProductId);
                isInOrder = (int)cmd.ExecuteScalar() > 0;
            }
            if (isInOrder) { MessageBox.Show("Нельзя удалить товар, который есть в заказах!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); return; }

            if (MessageBox.Show($"Удалить товар \"{selectedProd.Name}\"?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try
                {
                    // Удаление фото из папки
                    if (!string.IsNullOrEmpty(selectedProd.PhotoFileName))
                    { string photoPath = Path.Combine(imageDirectory, selectedProd.PhotoFileName); if (File.Exists(photoPath)) File.Delete(photoPath); }
                    // Удаление из БД
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        SqlCommand cmd = new SqlCommand("DELETE FROM Products WHERE ProductID = @id", conn);
                        cmd.Parameters.AddWithValue("@id", selectedProductId);
                        cmd.ExecuteNonQuery();
                    }
                    MessageBox.Show("Товар удален!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    selectedProductId = 0;
                    LoadProducts();
                }
                catch (Exception ex) { MessageBox.Show("Ошибка: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); }
            }
        }

        // Добавление заказа
        private void btnAddOrder_Click(object sender, RoutedEventArgs e)
        {
            foreach (Window window in Application.Current.Windows)
                if (window is Window3) { MessageBox.Show("Окно создания заказа уже открыто!", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            Window3 orderWnd = new Window3(connectionString, 0);
            orderWnd.Owner = this;
            if (orderWnd.ShowDialog() == true && !showingProducts) LoadOrders();
        }

        // Редактирование заказа
        private void btnEditOrder_Click(object sender, RoutedEventArgs e)
        {
            if (showingProducts) { MessageBox.Show("Переключитесь на вкладку 'Заказы'", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            int selectedOrderId = 0;
            foreach (Border card in pnlCards.Children)
                if (card is Border && card.BorderBrush == Brushes.Blue && card.Tag != null) { selectedOrderId = (int)card.Tag; break; }
            if (selectedOrderId == 0) { MessageBox.Show("Выберите заказ (нажмите на карточку)", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            foreach (Window window in Application.Current.Windows)
                if (window is Window3) { MessageBox.Show("Окно редактирования заказа уже открыто!", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            Window3 editWnd = new Window3(connectionString, selectedOrderId);
            editWnd.Owner = this;
            if (editWnd.ShowDialog() == true) LoadOrders();
        }

        // Удаление заказа
        private void btnDeleteOrder_Click(object sender, RoutedEventArgs e)
        {
            if (showingProducts) { MessageBox.Show("Переключитесь на вкладку 'Заказы'", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            int selectedOrderId = 0;
            int selectedOrderNumber = 0;
            foreach (Border card in pnlCards.Children)
            {
                if (card is Border && card.BorderBrush == Brushes.Blue && card.Tag != null)
                {
                    selectedOrderId = (int)card.Tag;
                    StackPanel panel = card.Child as StackPanel;
                    if (panel != null && panel.Children.Count > 1)
                    {
                        TextBlock numberBlock = panel.Children[1] as TextBlock;
                        if (numberBlock != null)
                        {
                            string numText = numberBlock.Text.Replace("Заказ №", "");
                            int.TryParse(numText, out selectedOrderNumber);
                        }
                    }
                    break;
                }
            }
            if (selectedOrderId == 0) { MessageBox.Show("Выберите заказ (нажмите на карточку)", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (MessageBox.Show($"Удалить заказ №{selectedOrderNumber}?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    SqlCommand delProdCmd = new SqlCommand("DELETE FROM OrderProducts WHERE OrderID = @id", conn);
                    delProdCmd.Parameters.AddWithValue("@id", selectedOrderId);
                    delProdCmd.ExecuteNonQuery();
                    SqlCommand delOrderCmd = new SqlCommand("DELETE FROM Orders WHERE OrderID = @id", conn);
                    delOrderCmd.Parameters.AddWithValue("@id", selectedOrderId);
                    delOrderCmd.ExecuteNonQuery();
                }
                MessageBox.Show("Заказ удален!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadOrders();
            }
        }
    }
}