using System;
using System.Data.SqlClient;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace StroyMaterials
{
    public partial class MainWindow : Window
    {
   
        private string connectionString = @"Server=ASUS\SQLEXPRESS;Database=StroyMaterials;Integrated Security=True;";
        private string imageDirectory;

        
        public static string CurrentUserName { get; set; }
        public static int CurrentUserID { get; set; }
        public static string CurrentUserRole { get; set; }

        public MainWindow()
        {
            InitializeComponent();

            // Определение путя к папке с изображениями 
            string projectDirectory = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).Parent.Parent.FullName;
            imageDirectory = Path.Combine(projectDirectory, "img");

            LoadLogo();
        }

        // Загрузка логотипа из папки img
        private void LoadLogo()
        {
            try
            {
                string logoPath = Path.Combine(imageDirectory, "icon.png");
                if (!File.Exists(logoPath))
                {
                    logoPath = Path.Combine(imageDirectory, "logo.png");
                }
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

        // Обработчик кнопки "Войти"
        private void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            string login = txtLogin.Text.Trim();
            string password = txtPassword.Password;

            // Проверка на пустые поля
            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Введите логин и пароль!", "Ошибка авторизации",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    // Запрос: выбираем пользователя по логину/паролю и его роль
                    string query = @"
                        SELECT u.UserID, u.FullName, r.RoleName
                        FROM Users u
                        INNER JOIN Roles r ON u.RoleID = r.RoleID
                        WHERE u.Login = @Login AND u.Password = @Password";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Login", login);
                        cmd.Parameters.AddWithValue("@Password", password);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                // Сохрание данных пользователя
                                CurrentUserID = Convert.ToInt32(reader["UserID"]);
                                CurrentUserName = reader["FullName"].ToString();
                                CurrentUserRole = reader["RoleName"].ToString();

               
                                MessageBox.Show($"Добро пожаловать, {CurrentUserName}!",
                                    "Успешный вход", MessageBoxButton.OK, MessageBoxImage.Information);

                          
                                Window1 mainWnd = new Window1();
                                mainWnd.Show();
                                Close();
                            }
                            else
                            {
                                MessageBox.Show("Неверный логин или пароль!", "Ошибка авторизации",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка подключения к базе данных: " + ex.Message,
                    "Критическая ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Обработчик кнопки "Гость"
        private void btnGuest_Click(object sender, RoutedEventArgs e)
        {
            // Заполнение данных гостя
            CurrentUserName = "Гость";
            CurrentUserID = 0;
            CurrentUserRole = "Гость";

            MessageBox.Show("Вы вошли как гость. Доступен только просмотр товаров.",
                "Гостевой режим", MessageBoxButton.OK, MessageBoxImage.Information);

            Window1 mainWnd = new Window1();
            mainWnd.Show();
            Close();
        }
    }
}