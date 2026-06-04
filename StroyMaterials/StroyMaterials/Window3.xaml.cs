using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;

namespace StroyMaterials
{
    public partial class Window3 : Window
    {
        private string connectionString;
        private int orderId;
        private List<OrderProduct> selectedProducts = new List<OrderProduct>();

      
        public class OrderProduct
        {
            public int ProductID { get; set; }
            public string ProductName { get; set; }
            public int Quantity { get; set; }
            public decimal Price { get; set; }
            public string Article { get; set; }
        }

        public Window3(string connStr, int id)
        {
            // Блокировка открытия нескольких окон
            foreach (Window window in Application.Current.Windows)
                if (window is Window3 && window != this)
                { MessageBox.Show("Окно заказа уже открыто!", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning); DialogResult = false; Close(); return; }

            InitializeComponent();
            connectionString = connStr;
            orderId = id;
            LoadData();

            if (orderId > 0)
            {
                LoadOrder();
                Title = "Редактирование заказа";
                btnSave.Content = "Сохранить изменения";
            }
            else
            {
                Title = "Новый заказ";
                dpDelivery.SelectedDate = DateTime.Now.AddDays(14);
            }
        }

        // Загрузка выпадающих списков
        private void LoadData()
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlDataAdapter clientAdapter = new SqlDataAdapter("SELECT UserID, FullName FROM Users WHERE RoleID = 3", conn);
                DataTable dtClients = new DataTable();
                clientAdapter.Fill(dtClients);
                cmbClient.ItemsSource = dtClients.DefaultView;
                cmbClient.DisplayMemberPath = "FullName";
                cmbClient.SelectedValuePath = "UserID";

                SqlDataAdapter pickupAdapter = new SqlDataAdapter("SELECT PointID, Address FROM PickupPoints", conn);
                DataTable dtPickup = new DataTable();
                pickupAdapter.Fill(dtPickup);
                cmbPickup.ItemsSource = dtPickup.DefaultView;
                cmbPickup.DisplayMemberPath = "Address";
                cmbPickup.SelectedValuePath = "PointID";
            }
        }

        // Загрузка заказа при редактировании
        private void LoadOrder()
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                // Основная информация
                string orderQuery = "SELECT UserID, PickupPointID, DeliveryDate, StatusID FROM Orders WHERE OrderID = @id";
                SqlCommand orderCmd = new SqlCommand(orderQuery, conn);
                orderCmd.Parameters.AddWithValue("@id", orderId);
                using (SqlDataReader reader = orderCmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        cmbClient.SelectedValue = reader["UserID"];
                        cmbPickup.SelectedValue = reader["PickupPointID"];
                        dpDelivery.SelectedDate = reader["DeliveryDate"] != DBNull.Value ? Convert.ToDateTime(reader["DeliveryDate"]) : (DateTime?)null;
                        int statusId = Convert.ToInt32(reader["StatusID"]);
                        cmbStatus.SelectedIndex = statusId - 1;
                    }
                }

                // Товары в заказе
                string productsQuery = @"
                    SELECT op.ProductID, op.Quantity, op.PriceAtMoment, p.Name as ProductName, p.Article
                    FROM OrderProducts op JOIN Products p ON op.ProductID = p.ProductID
                    WHERE op.OrderID = @id";
                SqlCommand productsCmd = new SqlCommand(productsQuery, conn);
                productsCmd.Parameters.AddWithValue("@id", orderId);
                using (SqlDataReader reader = productsCmd.ExecuteReader())
                {
                    selectedProducts.Clear();
                    while (reader.Read())
                        selectedProducts.Add(new OrderProduct
                        {
                            ProductID = reader.GetInt32(0),
                            Quantity = reader.GetInt32(1),
                            Price = reader.GetDecimal(2),
                            ProductName = reader.GetString(3),
                            Article = reader.GetString(4)
                        });
                }
                UpdateProductsList();
            }
        }

        // Поиск товара по артикулу в БД
        private DataRowView GetProductByArticle(string article)
        {
            DataTable products = new DataTable();
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand("SELECT ProductID, Name, Price, Article FROM Products WHERE Article = @art", conn);
                cmd.Parameters.AddWithValue("@art", article);
                SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                adapter.Fill(products);
            }
            return products.Rows.Count > 0 ? products.DefaultView[0] : null;
        }

        // Добавление товара по артикулу
        private void btnAddProduct_Click(object sender, RoutedEventArgs e)
        {
            string article = txtArticle.Text.Trim();
            if (string.IsNullOrEmpty(article))
            {
                MessageBox.Show("Введите артикул товара", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!int.TryParse(txtQuantity.Text, out int quantity) || quantity <= 0)
            {
                MessageBox.Show("Введите корректное количество", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DataRowView product = GetProductByArticle(article);
            if (product == null)
            {
                MessageBox.Show($"Товар с артикулом '{article}' не найден!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int productId = Convert.ToInt32(product["ProductID"]);
            string productName = product["Name"].ToString();
            decimal price = Convert.ToDecimal(product["Price"]);
            string foundArticle = product["Article"].ToString();

            var existing = selectedProducts.Find(p => p.ProductID == productId);
            if (existing != null) existing.Quantity += quantity;
            else selectedProducts.Add(new OrderProduct
            {
                ProductID = productId,
                ProductName = productName,
                Quantity = quantity,
                Price = price,
                Article = foundArticle
            });

            UpdateProductsList();
            txtArticle.Text = "";
            txtQuantity.Text = "1";
        }

        private void RemoveProduct_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            OrderProduct product = btn.Tag as OrderProduct;
            if (product != null)
            {
                selectedProducts.Remove(product);
                UpdateProductsList();
            }
        }

        private void UpdateProductsList()
        {
            lvProducts.ItemsSource = null;
            lvProducts.ItemsSource = selectedProducts;
        }

        // Сохранение заказа
        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            // Валидация
            if (cmbClient.SelectedItem == null)
            {
                MessageBox.Show("Выберите клиента", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (cmbPickup.SelectedItem == null)
            {
                MessageBox.Show("Выберите пункт выдачи", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (dpDelivery.SelectedDate == null)
            {
                MessageBox.Show("Выберите дату доставки", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (selectedProducts.Count == 0)
            {
                MessageBox.Show("Добавьте хотя бы один товар", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    SqlTransaction transaction = conn.BeginTransaction();
                    try
                    {
                        int statusId = cmbStatus.SelectedIndex + 1;
                        if (orderId == 0)
                        {
                            // НОВЫЙ ЗАКАЗ
                            SqlCommand getMaxCmd = new SqlCommand("SELECT ISNULL(MAX(OrderNumber), 0) + 1 FROM Orders", conn, transaction);
                            int newNumber = Convert.ToInt32(getMaxCmd.ExecuteScalar());

                            string insertOrderQuery = @"INSERT INTO Orders (OrderNumber, OrderDate, DeliveryDate, PickupPointID, UserID, PickupCode, StatusID)
                                                        VALUES (@num, @date, @delivery, @point, @user, @code, @status);
                                                        SELECT SCOPE_IDENTITY();";
                            SqlCommand insertOrderCmd = new SqlCommand(insertOrderQuery, conn, transaction);
                            insertOrderCmd.Parameters.AddWithValue("@num", newNumber);
                            insertOrderCmd.Parameters.AddWithValue("@date", DateTime.Now);
                            insertOrderCmd.Parameters.AddWithValue("@delivery", dpDelivery.SelectedDate.Value);
                            insertOrderCmd.Parameters.AddWithValue("@point", cmbPickup.SelectedValue);
                            insertOrderCmd.Parameters.AddWithValue("@user", cmbClient.SelectedValue);
                            insertOrderCmd.Parameters.AddWithValue("@code", new Random().Next(100, 999).ToString());
                            insertOrderCmd.Parameters.AddWithValue("@status", statusId);
                            int newOrderId = Convert.ToInt32(insertOrderCmd.ExecuteScalar());

                            foreach (var product in selectedProducts)
                            {
                                string insertProductQuery = @"INSERT INTO OrderProducts (OrderID, ProductID, Quantity, PriceAtMoment)
                                                              VALUES (@orderId, @productId, @qty, @price)";
                                SqlCommand addProductCmd = new SqlCommand(insertProductQuery, conn, transaction);
                                addProductCmd.Parameters.AddWithValue("@orderId", newOrderId);
                                addProductCmd.Parameters.AddWithValue("@productId", product.ProductID);
                                addProductCmd.Parameters.AddWithValue("@qty", product.Quantity);
                                addProductCmd.Parameters.AddWithValue("@price", product.Price);
                                addProductCmd.ExecuteNonQuery();

                                SqlCommand updateStockCmd = new SqlCommand("UPDATE Products SET StockQuantity = StockQuantity - @qty WHERE ProductID = @id", conn, transaction);
                                updateStockCmd.Parameters.AddWithValue("@qty", product.Quantity);
                                updateStockCmd.Parameters.AddWithValue("@id", product.ProductID);
                                updateStockCmd.ExecuteNonQuery();
                            }
                            transaction.Commit();
                            MessageBox.Show($"Заказ №{newNumber} создан!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            // РЕДАКТИРОВАНИЕ ЗАКАЗА
                            string updateOrderQuery = @"UPDATE Orders SET DeliveryDate=@delivery, PickupPointID=@point, UserID=@user, StatusID=@status WHERE OrderID=@id";
                            SqlCommand updateOrderCmd = new SqlCommand(updateOrderQuery, conn, transaction);
                            updateOrderCmd.Parameters.AddWithValue("@id", orderId);
                            updateOrderCmd.Parameters.AddWithValue("@delivery", dpDelivery.SelectedDate.Value);
                            updateOrderCmd.Parameters.AddWithValue("@point", cmbPickup.SelectedValue);
                            updateOrderCmd.Parameters.AddWithValue("@user", cmbClient.SelectedValue);
                            updateOrderCmd.Parameters.AddWithValue("@status", statusId);
                            updateOrderCmd.ExecuteNonQuery();

                            SqlCommand deleteProductsCmd = new SqlCommand("DELETE FROM OrderProducts WHERE OrderID = @id", conn, transaction);
                            deleteProductsCmd.Parameters.AddWithValue("@id", orderId);
                            deleteProductsCmd.ExecuteNonQuery();

                            foreach (var product in selectedProducts)
                            {
                                string insertProductQuery = @"INSERT INTO OrderProducts (OrderID, ProductID, Quantity, PriceAtMoment)
                                                              VALUES (@orderId, @productId, @qty, @price)";
                                SqlCommand addProductCmd = new SqlCommand(insertProductQuery, conn, transaction);
                                addProductCmd.Parameters.AddWithValue("@orderId", orderId);
                                addProductCmd.Parameters.AddWithValue("@productId", product.ProductID);
                                addProductCmd.Parameters.AddWithValue("@qty", product.Quantity);
                                addProductCmd.Parameters.AddWithValue("@price", product.Price);
                                addProductCmd.ExecuteNonQuery();

                                SqlCommand updateStockCmd = new SqlCommand("UPDATE Products SET StockQuantity = StockQuantity - @qty WHERE ProductID = @id", conn, transaction);
                                updateStockCmd.Parameters.AddWithValue("@qty", product.Quantity);
                                updateStockCmd.Parameters.AddWithValue("@id", product.ProductID);
                                updateStockCmd.ExecuteNonQuery();
                            }
                            transaction.Commit();
                            MessageBox.Show("Заказ обновлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        DialogResult = true;
                        Close();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw ex;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}