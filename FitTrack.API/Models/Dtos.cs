namespace FitTrack.API.Models;

// Body for POST /api/nutrition/log
public class LogFoodRequest
{
    public string FoodName { get; set; } = string.Empty;
    public double Grams { get; set; }
    public double Calories { get; set; }
    public double Protein { get; set; }
    public double Carbs { get; set; }
    public double Fat { get; set; }
    public MealType MealType { get; set; }
    public DateTime? LogDate { get; set; } // optional — geçmiş güne ekleme için, null = bugün
}

// GET /api/nutrition/summary
public class NutritionSummary
{
    public double TotalCalories { get; set; }
    public double TotalProtein { get; set; }
    public double TotalCarbs { get; set; }
    public double TotalFat { get; set; }
}

// GET /api/nutrition/history → one entry per day
public class DailyNutritionSummary
{
    public DateTime Date { get; set; }
    public double TotalCalories { get; set; }
    public double TotalProtein { get; set; }
    public double TotalCarbs { get; set; }
    public double TotalFat { get; set; }
    public int MealCount { get; set; }
}

// GET /api/nutrition/streak
public class NutritionStreak
{
    public int Days { get; set; }
}

// Body for PUT /api/goals
public class UpdateGoalsRequest
{
    public double CalorieGoal { get; set; }
    public double ProteinGoal { get; set; }
    public double CarbGoal { get; set; }
    public double FatGoal { get; set; }
}

// ---- Workout DTOs ----

public class CreateSessionRequest
{
    public string Name { get; set; } = string.Empty;
}

public class AddExerciseRequest
{
    public Guid WorkoutSessionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string MuscleGroup { get; set; } = string.Empty;
}

// Body for PUT /api/workout/exercise/{id}
public class UpdateExerciseRequest
{
    public string Name { get; set; } = string.Empty;
    public string MuscleGroup { get; set; } = string.Empty;
}

public class AddSetRequest
{
    public Guid ExerciseId { get; set; }
    public int SetNumber { get; set; }
    public double WeightKg { get; set; }
    public int Reps { get; set; }
    public bool IsCompleted { get; set; }
}

public class UpdateSetRequest
{
    public double WeightKg { get; set; }
    public int Reps { get; set; }
    public bool IsCompleted { get; set; }
}

// Row for GET /api/workout/sessions
public class SessionSummary
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime LoggedAt { get; set; }
    public int ExerciseCount { get; set; }
}

// Row for GET /api/workout/exercise/{name}/history
public class ExerciseHistoryItem
{
    public Guid SessionId { get; set; }
    public string SessionName { get; set; } = string.Empty;
    public DateTime LoggedAt { get; set; }
    public string MuscleGroup { get; set; } = string.Empty;
    public List<ExerciseSet> Sets { get; set; } = new();
}

// Group for GET /api/workout/exercises/list
public class MuscleGroupExercises
{
    public string MuscleGroup { get; set; } = string.Empty;
    public List<string> Exercises { get; set; } = new();
}

// ---- Weight DTOs ----

public class LogWeightRequest
{
    public double WeightKg { get; set; }
    public string? Notes { get; set; }
    public DateTime? LoggedAt { get; set; } // optional — defaults to now if not provided
}

// Point for GET /api/weight/history (chart-friendly)
public class WeightPoint
{
    public DateTime LoggedAt { get; set; }
    public double WeightKg { get; set; }
}

// GET /api/weight/stats
public class WeightStats
{
    public double? CurrentWeight { get; set; }
    public double? StartWeight { get; set; }
    public double? LowestWeight { get; set; }
    public double? HighestWeight { get; set; }
    public double? TotalChange { get; set; }
    public double? WeeklyChange { get; set; }
    public DateTime? LastLoggedAt { get; set; }
}

// ---- Check-in DTOs ----

public class LogCheckInRequest
{
    public int Mood { get; set; }
    public int Energy { get; set; }
    public int Hunger { get; set; }
    public string? Note { get; set; }
    public string? Context { get; set; }
}

// ---- Profile DTO ----

public class UpdateProfileRequest
{
    public double? HeightCm { get; set; }
    public double? TargetWeightKg { get; set; }
}

// ---- Coach (AI) DTOs ----

public class CoachMessage
{
    public string Role { get; set; } = "user"; // "user" | "assistant"
    public string Content { get; set; } = string.Empty;
}

// Body for POST /api/coach/chat — the client sends the running conversation.
public class CoachChatRequest
{
    public List<CoachMessage> Messages { get; set; } = new();
}

public class CoachChatResponse
{
    public string Reply { get; set; } = string.Empty;
    // Domains the coach mutated this turn (e.g. "nutrition") so the client can refresh.
    public List<string> Actions { get; set; } = new();
}
