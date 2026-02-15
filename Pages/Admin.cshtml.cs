using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using HelloWorldWeb.Models;
using HelloWorldWeb.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HelloWorldWeb.Pages
{
    public class AdminModel : PageModel
    {
        private readonly AuthService _authService;
        private readonly QuestionDifficultyService _difficultyService;
        private readonly SupabaseStorageService _storage;

        public AdminModel(AuthService authService, QuestionDifficultyService difficultyService = null, SupabaseStorageService storage = null)
        {
            _authService = authService;
            _difficultyService = difficultyService;
            _storage = storage;
        }

        public List<User> AllUsers { get; set; } = new();
        public List<User> Cheaters { get; set; } = new();
        public List<User> BannedUsers { get; set; } = new();
        public List<User> OnlineUsers { get; set; } = new();
        public List<User> TopUsers { get; set; } = new();
        public double AverageSuccessRate { get; set; }
        
        public List<QuestionDifficulty> DifficultyQuestions { get; set; } = new();
        public int EasyCount { get; set; }
        public int MediumCount { get; set; }
        public int HardCount { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                await LoadData();
                await LoadDifficultyData();
                return Page();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Server error: {ex.Message}");
            }
        }

        private async Task LoadDifficultyData()
        {
            try
            {
                var difficultyByFile = new Dictionary<string, QuestionDifficulty>(StringComparer.OrdinalIgnoreCase);

                if (_difficultyService != null)
                {
                    var fromDb = await _difficultyService.GetAllQuestions(10000);
                    foreach (var q in fromDb.Where(q => !string.IsNullOrWhiteSpace(q.QuestionFile)))
                        difficultyByFile[q.QuestionFile] = q;
                }

                var allQuestionFiles = await GetAllQuestionFileNamesAsync();
                var fileSet = new HashSet<string>(allQuestionFiles, StringComparer.OrdinalIgnoreCase);
                var merged = new List<QuestionDifficulty>();

                foreach (var file in allQuestionFiles.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    if (difficultyByFile.TryGetValue(file, out var existing))
                        merged.Add(existing);
                    else
                        merged.Add(new QuestionDifficulty
                        {
                            QuestionFile = file,
                            Difficulty = "unrated",
                            SuccessRate = 0,
                            TotalAttempts = 0,
                            CorrectAttempts = 0,
                            LastUpdated = DateTime.MinValue,
                            CreatedAt = DateTime.MinValue
                        });
                }

                foreach (var kv in difficultyByFile.Where(kv => !fileSet.Contains(kv.Key)))
                    merged.Add(kv.Value);

                DifficultyQuestions = merged.OrderBy(q => q.QuestionFile, StringComparer.OrdinalIgnoreCase).ToList();
                EasyCount = DifficultyQuestions.Count(q => q.Difficulty == "easy");
                MediumCount = DifficultyQuestions.Count(q => q.Difficulty == "medium");
                HardCount = DifficultyQuestions.Count(q => q.Difficulty == "hard");
            }
            catch (Exception)
            {
                DifficultyQuestions = new List<QuestionDifficulty>();
            }
        }

        /// <summary>
        /// Returns all "question" file names (first image in each group of 5), from storage or local.
        /// </summary>
        private async Task<List<string>> GetAllQuestionFileNamesAsync()
        {
            var allImages = new List<string>();

            if (_storage != null)
            {
                try
                {
                    var files = await _storage.ListFilesAsync("");
                    allImages = files
                        .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }
                catch { /* fallback to local */ }
            }

            if (allImages.Count == 0)
            {
                var imagesDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "quiz_images");
                if (!Directory.Exists(imagesDir))
                    imagesDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");
                if (Directory.Exists(imagesDir))
                {
                    allImages = Directory.GetFiles(imagesDir)
                        .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                        .Select(Path.GetFileName)
                        .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }
            }

            var questionFiles = new List<string>();
            for (int i = 0; i < allImages.Count; i += 5)
            {
                if (i < allImages.Count && !string.IsNullOrWhiteSpace(allImages[i]))
                    questionFiles.Add(allImages[i]);
            }
            return questionFiles;
        }

        private async Task LoadData()
        {
            try
            {
                AllUsers = await _authService.GetAllUsers();
                Cheaters = AllUsers.Where(u => u.IsCheater).ToList();
                BannedUsers = AllUsers.Where(u => u.IsBanned).ToList();
                OnlineUsers = AllUsers.Where(u => u.LastSeen != null && u.LastSeen > DateTime.UtcNow.AddMinutes(-5)).ToList();
                TopUsers = AllUsers.OrderByDescending(u => u.CorrectAnswers).Take(5).ToList();
                // Calculate average success rate, excluding users with 0% success rate
                AverageSuccessRate = AllUsers
                    .Where(u => u.TotalAnswered > 0 && u.CorrectAnswers > 0)
                    .Select(u => (double)u.CorrectAnswers / u.TotalAnswered)
                    .DefaultIfEmpty(0).Average() * 100;
            }
            catch (Exception)
            {
                Cheaters = new List<User>();
                BannedUsers = new List<User>();
                TopUsers = new List<User>();
                OnlineUsers = new List<User>();
                AllUsers = new List<User>();
                AverageSuccessRate = 0;
            }
        }
    }
}
