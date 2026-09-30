using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace BigDataFootballAnalysis
{
    class Program
    {
        class Match
        {
            public string League { get; set; }
            public string DateStr { get; set; }
            public DateTime ParsedDate { get; set; }
            public string HomeTeam { get; set; }
            public string AwayTeam { get; set; }
            public string FTHG { get; set; }
            public string FTAG { get; set; }
            public string HTHG { get; set; }
            public string HTAG { get; set; }
            public string HTR { get; set; }
            public string FTR { get; set; }

            public string Open1 { get; set; }
            public string OpenX { get; set; }
            public string Open2 { get; set; }
            public string Close1 { get; set; }
            public string CloseX { get; set; }
            public string Close2 { get; set; }
        }

        static void Main(string[] args)
        {
            string path = @"C:\Users\YUSUF\Downloads\maç verileri";

            if (!Directory.Exists(path))
            {
                Console.WriteLine("HATA: Belirtilen klasör bulunamadı!");
                Console.ReadLine();
                return;
            }

            string[] csvFiles = Directory.GetFiles(path, "*.csv");
            if (csvFiles.Length == 0)
            {
                Console.WriteLine("Bu klasörde hiçbir .csv dosyası bulunamadı.");
                Console.ReadLine();
                return;
            }

            Console.WriteLine($"{csvFiles.Length} adet lig/sezon dosyası taranıyor. Lütfen bekleyin...");

            List<Match> allMatches = new List<Match>();
            string[] dateFormats = { "dd/MM/yyyy", "d/M/yyyy", "dd/MM/yy", "d/M/yy" };

            // 1. AŞAMA: DOSYALARI OKU
            foreach (string file in csvFiles)
            {
                string leagueName = Path.GetFileNameWithoutExtension(file).ToUpper();

                try
                {
                    string[] lines = File.ReadAllLines(file);
                    if (lines.Length <= 1) continue;

                    string[] headers = lines[0].Split(',');

                    int dateIdx = Array.IndexOf(headers, "Date");
                    int homeIdx = Array.IndexOf(headers, "HomeTeam");
                    int awayIdx = Array.IndexOf(headers, "AwayTeam");
                    int fthgIdx = Array.IndexOf(headers, "FTHG");
                    int ftagIdx = Array.IndexOf(headers, "FTAG");
                    int hthgIdx = Array.IndexOf(headers, "HTHG");
                    int htagIdx = Array.IndexOf(headers, "HTAG");
                    int htrIdx = Array.IndexOf(headers, "HTR");
                    int ftrIdx = Array.IndexOf(headers, "FTR");

                    int o1Idx = Array.IndexOf(headers, "B365H");
                    int oxIdx = Array.IndexOf(headers, "B365D");
                    int o2Idx = Array.IndexOf(headers, "B365A");
                    int c1Idx = Array.IndexOf(headers, "B365CH");
                    int cxIdx = Array.IndexOf(headers, "B365CD");
                    int c2Idx = Array.IndexOf(headers, "B365CA");

                    if (dateIdx == -1 || homeIdx == -1 || fthgIdx == -1 || htrIdx == -1) continue;

                    for (int i = 1; i < lines.Length; i++)
                    {
                        if (string.IsNullOrWhiteSpace(lines[i])) continue;
                        string[] cols = lines[i].Split(',');
                        if (cols.Length <= Math.Max(htrIdx, ftrIdx)) continue;

                        DateTime matchDate;
                        bool isValidDate = DateTime.TryParseExact(cols[dateIdx], dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out matchDate);

                        if (!isValidDate) continue;

                        allMatches.Add(new Match
                        {
                            League = leagueName,
                            DateStr = cols[dateIdx],
                            ParsedDate = matchDate,
                            HomeTeam = cols[homeIdx],
                            AwayTeam = cols[awayIdx],
                            FTHG = cols[fthgIdx],
                            FTAG = cols[ftagIdx],
                            HTHG = hthgIdx != -1 ? cols[hthgIdx] : "?",
                            HTAG = htagIdx != -1 ? cols[htagIdx] : "?",
                            HTR = cols[htrIdx],
                            FTR = cols[ftrIdx],
                            Open1 = o1Idx != -1 && cols.Length > o1Idx ? cols[o1Idx] : "-",
                            OpenX = oxIdx != -1 && cols.Length > oxIdx ? cols[oxIdx] : "-",
                            Open2 = o2Idx != -1 && cols.Length > o2Idx ? cols[o2Idx] : "-",
                            Close1 = c1Idx != -1 && cols.Length > c1Idx ? cols[c1Idx] : "-",
                            CloseX = cxIdx != -1 && cols.Length > cxIdx ? cols[cxIdx] : "-",
                            Close2 = c2Idx != -1 && cols.Length > c2Idx ? cols[c2Idx] : "-"
                        });
                    }
                }
                catch { }
            }

            // 2. AŞAMA: 1/2 VE 2/1 MAÇLARINI FİLTRELE
            var turnarounds = allMatches
                .Where(m => (m.HTR == "H" && m.FTR == "A") || (m.HTR == "A" && m.FTR == "H"))
                .OrderBy(m => m.ParsedDate)
                .ToList();

            Console.WriteLine($"Taranan {allMatches.Count} maçtan {turnarounds.Count} tanesi 1/2 veya 2/1 bitti.");
            Console.WriteLine("Skor eşleşmeleri ve altındaki maçlar listeleniyor...");

            Dictionary<string, int> prevScorePairsCount = new Dictionary<string, int>();
            // *** YENİ EKLENEN KISIM: Hangi skordan hangi maçların çıktığını tutan sözlük ***
            Dictionary<string, List<string>> matchesUnderScorePairs = new Dictionary<string, List<string>>();

            Dictionary<string, int> openOddsCount = new Dictionary<string, int>();
            Dictionary<string, int> closeOddsCount = new Dictionary<string, int>();

            List<string> reportLines = new List<string>();

            // 3. AŞAMA: ÖNCEKİ MAÇLARI VE ÇIKAN SONUÇLARI KAYDET
            foreach (var match in turnarounds)
            {
                var homePrev = allMatches
                    .Where(m => m.League == match.League && (m.HomeTeam == match.HomeTeam || m.AwayTeam == match.HomeTeam) && m.ParsedDate < match.ParsedDate)
                    .OrderByDescending(m => m.ParsedDate)
                    .FirstOrDefault();

                var awayPrev = allMatches
                    .Where(m => m.League == match.League && (m.HomeTeam == match.AwayTeam || m.AwayTeam == match.AwayTeam) && m.ParsedDate < match.ParsedDate)
                    .OrderByDescending(m => m.ParsedDate)
                    .FirstOrDefault();

                if (homePrev != null && awayPrev != null)
                {
                    string homeTeamScore = GetTeamFormScore(homePrev, match.HomeTeam);
                    string awayTeamScore = GetTeamFormScore(awayPrev, match.AwayTeam);

                    string[] pair = { homeTeamScore, awayTeamScore };
                    Array.Sort(pair);
                    string pairKey = $"{pair[0]} & {pair[1]}";

                    if (!prevScorePairsCount.ContainsKey(pairKey))
                    {
                        prevScorePairsCount[pairKey] = 0;
                        matchesUnderScorePairs[pairKey] = new List<string>();
                    }

                    prevScorePairsCount[pairKey]++;

                    // Maçın detayını listeye ekliyoruz
                    string turnType = match.HTR == "H" ? "1/2" : "2/1";
                    string matchDetail = $"   -> {match.DateStr} | LİG: {match.League} | {match.HomeTeam} {match.FTHG}-{match.FTAG} {match.AwayTeam} (İY: {match.HTHG}-{match.HTAG}) [{turnType}]";
                    matchesUnderScorePairs[pairKey].Add(matchDetail);
                }

                if (match.Open1 != "-" && match.OpenX != "-" && match.Open2 != "-")
                {
                    string openKey = $"{match.Open1} - {match.OpenX} - {match.Open2}";
                    if (!openOddsCount.ContainsKey(openKey)) openOddsCount[openKey] = 0;
                    openOddsCount[openKey]++;
                }

                if (match.Close1 != "-" && match.CloseX != "-" && match.Close2 != "-")
                {
                    string closeKey = $"{match.Close1} - {match.CloseX} - {match.Close2}";
                    if (!closeOddsCount.ContainsKey(closeKey)) closeOddsCount[closeKey] = 0;
                    closeOddsCount[closeKey]++;
                }
            }

            // 4. AŞAMA: DETAYLI RAPORU TXT'YE YAZDIR
            reportLines.Add("==========================================================");
            reportLines.Add("      DETAYLI: 1/2 VE 2/1 BİTEN MAÇLARIN GEÇMİŞİ");
            reportLines.Add("==========================================================\n");

            reportLines.Add("--- İKİ TAKIMIN BİR ÖNCEKİ MAÇ SKORLARI VE OYNANAN MAÇLAR ---");
            reportLines.Add("Açıklama: Takımların o maçtan bir hafta önce (Attığı - Yediği) gol sayıları ve ardından çıkan 1/2 - 2/1 maçları.\n");

            // Sadece en çok tekrar eden ilk 30 skor kombinasyonunu listeliyoruz
            var sortedPairs = prevScorePairsCount.OrderByDescending(x => x.Value).Take(30);
            foreach (var sp in sortedPairs)
            {
                reportLines.Add("----------------------------------------------------------");
                reportLines.Add($"📌 [Geçen Hafta] Biri {sp.Key.Split('&')[0].Trim()} / Diğeri {sp.Key.Split('&')[1].Trim()} Yapmış -> Bu eşleşme {sp.Value} KEZ 1/2 veya 2/1 getirmiş.");
                reportLines.Add("----------------------------------------------------------");

                // O skorlardan sonra oynanan tüm maçları alt alta yazdırıyoruz
                foreach (var matchDetail in matchesUnderScorePairs[sp.Key])
                {
                    reportLines.Add(matchDetail);
                }
                reportLines.Add(""); // Boşluk bırak
            }

            reportLines.Add("\n==========================================================");
            reportLines.Add("--- EN ÇOK TEKRAR EDEN BİREBİR AÇILIŞ ORANLARI ---");
            var sortedOpens = openOddsCount.OrderByDescending(x => x.Value).Take(30);
            foreach (var so in sortedOpens)
            {
                if (so.Value > 1)
                    reportLines.Add($"Açılış: {so.Key.Replace(".", ",")} -> Tam {so.Value} Kez 1/2 veya 2/1 Getirmiş");
            }

            reportLines.Add("\n--- EN ÇOK TEKRAR EDEN BİREBİR KAPANIŞ ORANLARI ---");
            var sortedCloses = closeOddsCount.OrderByDescending(x => x.Value).Take(30);
            foreach (var sc in sortedCloses)
            {
                if (sc.Value > 1)
                    reportLines.Add($"Kapanış: {sc.Key.Replace(".", ",")} -> Tam {sc.Value} Kez 1/2 veya 2/1 Getirmiş");
            }

            string outPath = Path.Combine(path, "Geri_Donus_Analiz_Sonuclari_Detayli.txt");
            File.WriteAllLines(outPath, reportLines);

            Console.WriteLine($"\nRAPOR OLUŞTURULDU!");
            Console.WriteLine($"Sonuçlar şu dosyaya kaydedildi: {outPath}");
            Console.WriteLine("Çıkmak için Enter'a basın...");
            Console.ReadLine();
        }

        static string GetTeamFormScore(Match m, string teamName)
        {
            if (m.HomeTeam == teamName) return $"{m.FTHG}-{m.FTAG}";
            else return $"{m.FTAG}-{m.FTHG}";
        }
    }
}