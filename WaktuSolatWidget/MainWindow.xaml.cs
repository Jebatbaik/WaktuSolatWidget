using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Globalization;

namespace WaktuSolatWidget
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer? _apiTimer;
        private DispatcherTimer? _clockTimer;
        private readonly HttpClient _httpClient = new HttpClient();

        // Path to save the user's preferred zone so it remembers next time
        private readonly string _saveFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WaktuSolatZone.txt");
        private string _currentZoneCode = "WLY01"; // Default to KL

        public MainWindow()
        {
            InitializeComponent();
            LoadZoneList();
            LoadSavedZone();
            SetupTimers();
            FetchPrayerTimes();
        }

        // --- NEW: Populate the Dropdown with Malaysian Zones ---
        private void LoadZoneList()
        {
            var zones = new List<ZoneItem>
            {
                new ZoneItem { Name = "KL / Putrajaya", Code = "WLY01" },
                new ZoneItem { Name = "Selangor (S.Alam/Petaling)", Code = "SGR01" },
                new ZoneItem { Name = "Selangor (Klang/K.Langat)", Code = "SGR03" },
                new ZoneItem { Name = "Johor Bahru", Code = "JHR02" },
                new ZoneItem { Name = "Penang", Code = "PNG01" },
                new ZoneItem { Name = "Melaka", Code = "MLK01" },
                new ZoneItem { Name = "Negeri Sembilan", Code = "NGS02" },
                new ZoneItem { Name = "Perak (Ipoh)", Code = "PRK02" },
                new ZoneItem { Name = "Kedah (Alor Setar)", Code = "KDH01" },
                new ZoneItem { Name = "Pahang (Kuantan)", Code = "PHG02" },
                new ZoneItem { Name = "Terengganu (K.Trg)", Code = "TRG01" },
                new ZoneItem { Name = "Kelantan (Kota Bharu)", Code = "KTN01" },
                new ZoneItem { Name = "Sarawak (Kuching)", Code = "SWK08" },
                new ZoneItem { Name = "Sabah (Kota Kinabalu)", Code = "SBH05" }
            };

            cmbZone.ItemsSource = zones;
            cmbZone.DisplayMemberPath = "Name";
            cmbZone.SelectedValuePath = "Code";
        }

        // --- NEW: Load Memory ---
        private void LoadSavedZone()
        {
            if (File.Exists(_saveFilePath))
            {
                string savedCode = File.ReadAllText(_saveFilePath);
                if (!string.IsNullOrEmpty(savedCode))
                {
                    _currentZoneCode = savedCode;
                }
            }
            // Set the dropdown to match the saved zone
            cmbZone.SelectedValue = _currentZoneCode;
        }

        // --- NEW: When User changes the Dropdown ---
        private void CmbZone_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (cmbZone.SelectedValue != null)
            {
                _currentZoneCode = cmbZone.SelectedValue.ToString() ?? "WLY01";

                // Save choice to file
                File.WriteAllText(_saveFilePath, _currentZoneCode);

                // Fetch new times immediately
                FetchPrayerTimes();
            }
        }

        // --- NEW: Close Button Logic ---
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void SetupTimers()
        {
            _apiTimer = new DispatcherTimer();
            _apiTimer.Interval = TimeSpan.FromHours(4);
            _apiTimer.Tick += (s, e) => FetchPrayerTimes();
            _apiTimer.Start();

            _clockTimer = new DispatcherTimer();
            _clockTimer.Interval = TimeSpan.FromSeconds(1);
            _clockTimer.Tick += ClockTimer_Tick;
            _clockTimer.Start();

            ClockTimer_Tick(null, EventArgs.Empty);
        }

        private void ClockTimer_Tick(object? sender, EventArgs e)
        {
            lblCurrentTime.Text = DateTime.Now.ToString("hh:mm:ss tt");
            lblCurrentDate.Text = DateTime.Now.ToString("dddd, dd MMMM yyyy", new CultureInfo("ms-MY"));
        }

        private async void FetchPrayerTimes()
        {
            try
            {
                lblStatus.Text = "Updating...";

                string url = $"https://www.e-solat.gov.my/index.php?r=esolatApi/TakwimSolat&period=today&zone={_currentZoneCode}";

                HttpResponseMessage response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                string jsonResponse = await response.Content.ReadAsStringAsync();
                var prayerData = JsonSerializer.Deserialize<EsolatResponse>(jsonResponse);

                if (prayerData != null && prayerData.PrayerTimes != null && prayerData.PrayerTimes.Length > 0)
                {
                    var today = prayerData.PrayerTimes[0];

                    Dispatcher.Invoke(() =>
                    {
                        lblSubuh.Text = FormatTime(today.Fajr);
                        lblSyuruk.Text = FormatTime(today.Syuruk);
                        lblZohor.Text = FormatTime(today.Zuhr);
                        lblAsar.Text = FormatTime(today.Asr);
                        lblMaghrib.Text = FormatTime(today.Maghrib);
                        lblIsyak.Text = FormatTime(today.Isha);
                        lblStatus.Text = $"Dikemaskini: {DateTime.Now:HH:mm}";
                    });
                }
            }
            catch (Exception)
            {
                Dispatcher.Invoke(() =>
                {
                    lblStatus.Text = "Tiada sambungan internet.";
                });
            }
        }

        private string FormatTime(string? time24)
        {
            if (string.IsNullOrEmpty(time24)) return "--";

            if (TimeSpan.TryParse(time24, out TimeSpan time))
            {
                return new DateTime(time.Ticks).ToString("hh:mm tt");
            }
            return time24;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }
    }

    // --- JSON Mapping Classes ---
    public class ZoneItem
    {
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }

    public class EsolatResponse
    {
        [JsonPropertyName("prayerTime")]
        public PrayerTime[]? PrayerTimes { get; set; }
    }

    public class PrayerTime
    {
        [JsonPropertyName("fajr")] public string? Fajr { get; set; }
        [JsonPropertyName("syuruk")] public string? Syuruk { get; set; }
        [JsonPropertyName("dhuhr")] public string? Zuhr { get; set; }
        [JsonPropertyName("asr")] public string? Asr { get; set; }
        [JsonPropertyName("maghrib")] public string? Maghrib { get; set; }
        [JsonPropertyName("isha")] public string? Isha { get; set; }
    }
}