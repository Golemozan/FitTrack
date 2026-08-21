using FitTrack.API.Data;
using FitTrack.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.API.Controllers;

[ApiController]
[Route("api/workout")]
public class WorkoutController : ControllerBase
{
    private readonly AppDbContext _db;
    public WorkoutController(AppDbContext db) => _db = db;

    // 1. POST /api/workout/session
    [HttpPost("session")]
    public async Task<ActionResult<WorkoutSession>> CreateSession(CreateSessionRequest req)
    {
        var session = new WorkoutSession
        {
            Id = Guid.NewGuid(),
            Name = req.Name,
            LoggedAt = DateTime.Now,
        };
        _db.WorkoutSessions.Add(session);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetToday), null, session);
    }

    // 2. GET /api/workout/session/today
    [HttpGet("session/today")]
    public async Task<ActionResult<WorkoutSession?>> GetToday()
    {
        var day = DateTime.Now.Date;
        var session = await _db.WorkoutSessions
            .Include(w => w.Exercises).ThenInclude(e => e.Sets)
            .Where(w => w.LoggedAt >= day && w.LoggedAt < day.AddDays(1))
            .OrderByDescending(w => w.LoggedAt)
            .FirstOrDefaultAsync();
        // `Ok(null)` gövdesiz 204 döner; axios bunu `data: ""` yapar ve client'ta
        // `T | null` sözleşmesi bozulur (bkz. README GOTCHAS, 2026-08-20 siyah ekran).
        // JsonResult açıkça 200 + `null` gövdesi yazar.
        return new JsonResult(session);
    }

    // 2b. PUT /api/workout/session/{id}  → rename a session
    [HttpPut("session/{id:guid}")]
    public async Task<ActionResult<WorkoutSession>> RenameSession(Guid id, CreateSessionRequest req)
    {
        var session = await _db.WorkoutSessions
            .Include(w => w.Exercises).ThenInclude(e => e.Sets)
            .FirstOrDefaultAsync(w => w.Id == id);
        if (session is null) return NotFound();

        session.Name = req.Name;
        await _db.SaveChangesAsync();
        return Ok(session);
    }

    // 2c. DELETE /api/workout/session/{id}  (cascade removes exercises + sets)
    [HttpDelete("session/{id:guid}")]
    public async Task<IActionResult> DeleteSession(Guid id)
    {
        var session = await _db.WorkoutSessions.FindAsync(id);
        if (session is null) return NotFound();
        _db.WorkoutSessions.Remove(session);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // 3. GET /api/workout/sessions?limit=10
    [HttpGet("sessions")]
    public async Task<ActionResult<List<SessionSummary>>> GetSessions([FromQuery] int limit = 10)
    {
        if (limit <= 0) limit = 10;
        return await _db.WorkoutSessions
            .OrderByDescending(w => w.LoggedAt)
            .Take(limit)
            .Select(w => new SessionSummary
            {
                Id = w.Id,
                Name = w.Name,
                LoggedAt = w.LoggedAt,
                ExerciseCount = w.Exercises.Count,
            })
            .ToListAsync();
    }

    // 4. POST /api/workout/exercise
    [HttpPost("exercise")]
    public async Task<ActionResult<Exercise>> AddExercise(AddExerciseRequest req)
    {
        var sessionExists = await _db.WorkoutSessions.AnyAsync(w => w.Id == req.WorkoutSessionId);
        if (!sessionExists) return NotFound("Workout session not found");

        var exercise = new Exercise
        {
            Id = Guid.NewGuid(),
            WorkoutSessionId = req.WorkoutSessionId,
            Name = req.Name,
            MuscleGroup = req.MuscleGroup,
        };
        _db.Exercises.Add(exercise);
        await _db.SaveChangesAsync();
        return Ok(exercise);
    }

    // 4b. PUT /api/workout/exercise/{id}  → edit name / muscle group
    [HttpPut("exercise/{id:guid}")]
    public async Task<ActionResult<Exercise>> UpdateExercise(Guid id, UpdateExerciseRequest req)
    {
        var exercise = await _db.Exercises.Include(e => e.Sets).FirstOrDefaultAsync(e => e.Id == id);
        if (exercise is null) return NotFound();

        exercise.Name = req.Name;
        exercise.MuscleGroup = req.MuscleGroup;
        await _db.SaveChangesAsync();
        return Ok(exercise);
    }

    // 5. DELETE /api/workout/exercise/{id}  (cascade removes its sets)
    [HttpDelete("exercise/{id:guid}")]
    public async Task<IActionResult> DeleteExercise(Guid id)
    {
        var exercise = await _db.Exercises.FindAsync(id);
        if (exercise is null) return NotFound();
        _db.Exercises.Remove(exercise);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // 6. POST /api/workout/set
    [HttpPost("set")]
    public async Task<ActionResult<ExerciseSet>> AddSet(AddSetRequest req)
    {
        var exerciseExists = await _db.Exercises.AnyAsync(e => e.Id == req.ExerciseId);
        if (!exerciseExists) return NotFound("Exercise not found");

        var set = new ExerciseSet
        {
            Id = Guid.NewGuid(),
            ExerciseId = req.ExerciseId,
            SetNumber = req.SetNumber,
            WeightKg = req.WeightKg,
            Reps = req.Reps,
            IsCompleted = req.IsCompleted,
        };
        _db.ExerciseSets.Add(set);
        await _db.SaveChangesAsync();
        return Ok(set);
    }

    // 7. PUT /api/workout/set/{id}
    [HttpPut("set/{id:guid}")]
    public async Task<ActionResult<ExerciseSet>> UpdateSet(Guid id, UpdateSetRequest req)
    {
        var set = await _db.ExerciseSets.FindAsync(id);
        if (set is null) return NotFound();

        set.WeightKg = req.WeightKg;
        set.Reps = req.Reps;
        set.IsCompleted = req.IsCompleted;
        await _db.SaveChangesAsync();
        return Ok(set);
    }

    // 8. DELETE /api/workout/set/{id}
    [HttpDelete("set/{id:guid}")]
    public async Task<IActionResult> DeleteSet(Guid id)
    {
        var set = await _db.ExerciseSets.FindAsync(id);
        if (set is null) return NotFound();
        _db.ExerciseSets.Remove(set);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // 9. GET /api/workout/exercise/{name}/history
    //    Last 5 sessions where this exercise appeared, with its sets (ghost values).
    [HttpGet("exercise/{name}/history")]
    public async Task<ActionResult<List<ExerciseHistoryItem>>> ExerciseHistory(string name)
    {
        var items = await _db.Exercises
            .Where(e => e.Name == name)
            .Include(e => e.Sets)
            .Include(e => e.WorkoutSession)
            .OrderByDescending(e => e.WorkoutSession!.LoggedAt)
            .Take(5)
            .Select(e => new ExerciseHistoryItem
            {
                SessionId = e.WorkoutSessionId,
                SessionName = e.WorkoutSession!.Name,
                LoggedAt = e.WorkoutSession.LoggedAt,
                MuscleGroup = e.MuscleGroup,
                Sets = e.Sets.OrderBy(s => s.SetNumber).ToList(),
            })
            .ToListAsync();
        return items;
    }

    // 10. GET /api/workout/exercises/list  (hardcoded catalog by muscle group)
    [HttpGet("exercises/list")]
    public ActionResult<List<MuscleGroupExercises>> ExercisesList()
    {
        var catalog = new List<MuscleGroupExercises>
        {
            new() { MuscleGroup = "Göğüs", Exercises = new() {
                // Barbell
                "Bench Press (Barbell)", "Incline Bench Press", "Decline Bench Press",
                "Close-Grip Bench Press", "Floor Press",
                // Dumbbell
                "Dumbbell Press", "Incline Dumbbell Press", "Decline Dumbbell Press",
                "Dumbbell Fly", "Incline Dumbbell Fly", "Decline Dumbbell Fly",
                "Dumbbell Pullover",
                // Machine
                "Chest Press Machine", "Incline Chest Press Machine",
                "Pec Deck (Butterfly)", "Seated Fly Machine",
                // Cable
                "Cable Crossover (High)", "Cable Crossover (Mid)", "Cable Crossover (Low)",
                "Single Arm Cable Fly", "Cable Iron Cross",
                // Smith Machine
                "Smith Machine Bench Press", "Smith Machine Incline Press",
                // Bodyweight
                "Push-up", "Diamond Push-up", "Decline Push-up", "Incline Push-up",
                "Wide Grip Push-up", "Archer Push-up",
                // Dip
                "Chest Dip", "Weighted Chest Dip", "Machine Assisted Dip"
            }},
            new() { MuscleGroup = "Sırt", Exercises = new() {
                // Barbell
                "Deadlift", "Sumo Deadlift", "Romanian Deadlift", "Rack Pull",
                "Barbell Row", "Pendlay Row", "Yates Row",
                // Dumbbell
                "Dumbbell Row", "Meadows Row", "Chest Supported Dumbbell Row",
                "Dumbbell Pullover",
                // Machine
                "Lat Pulldown", "Close-Grip Lat Pulldown", "Wide-Grip Lat Pulldown",
                "Reverse Grip Lat Pulldown", "Cable Row", "T-Bar Row",
                "Straight Arm Pulldown", "Machine Row", "Hammer Strength Row",
                // Bodyweight
                "Pull-up", "Chin-up", "Neutral Grip Pull-up",
                "Weighted Pull-up", "Australian Pull-up",
                // Cable
                "Face Pull", "Reverse Fly (Cable)", "Cable Pullover",
                // Other
                "Kelso Shrug", "Rack Pull", "Good Morning"
            }},
            new() { MuscleGroup = "Bacak", Exercises = new() {
                // Squat variations
                "Squat (Barbell)", "Front Squat", "Goblet Squat", "Bulgarian Split Squat",
                "Hack Squat", "Sissy Squat", "Pistol Squat", "Box Squat",
                "Belt Squat", "Safety Bar Squat",
                // Machine
                "Leg Press", "45° Leg Press", "Hack Squat Machine",
                "Leg Extension", "Leg Curl (Seated)", "Leg Curl (Lying)",
                "Standing Leg Curl", "Adductor Machine", "Abductor Machine",
                // Smith Machine
                "Smith Machine Squat", "Smith Machine Split Squat",
                // Hinge
                "Romanian Deadlift", "Stiff-Leg Deadlift", "Good Morning",
                // Glute
                "Glute Bridge", "Hip Thrust", "Cable Kickback",
                "Glute Ham Raise", "Donkey Kick",
                // Lunge
                "Lunges", "Walking Lunges", "Reverse Lunges", "Lateral Lunges",
                "Step-up", "Curtsy Lunge",
                // Calf
                "Calf Raise (Standing)", "Seated Calf Raise", "Donkey Calf Raise",
                "Leg Press Calf Raise", "Single Leg Calf Raise",
                // Other
                "Nordic Curl", "Box Jump", "Sled Push", "Sled Pull"
            }},
            new() { MuscleGroup = "Omuz", Exercises = new() {
                // Barbell
                "Overhead Press (Barbell)", "Push Press", "Behind Neck Press",
                "Barbell Shrug",
                // Dumbbell
                "Dumbbell Shoulder Press", "Arnold Press", "Lateral Raise",
                "Front Raise", "Rear Delt Fly", "Dumbbell Shrug",
                "Bent-Over Lateral Raise", "Lu Raise", "Z Press",
                // Machine
                "Shoulder Press Machine", "Lateral Raise Machine",
                "Rear Delt Machine", "Smith Machine Overhead Press",
                // Cable
                "Cable Lateral Raise", "Cable Front Raise", "Face Pull",
                "Cable Upright Row", "High Pull",
                // Other
                "Upright Row (Barbell)", "Upright Row (Dumbbell)",
                "Plate Raise", "Iron Cross Hold"
            }},
            new() { MuscleGroup = "Kol", Exercises = new() {
                // Biceps
                "Barbell Curl", "EZ Bar Curl", "Dumbbell Curl", "Hammer Curl",
                "Incline Dumbbell Curl", "Concentration Curl", "Preacher Curl",
                "Cable Curl", "Spider Curl", "Machine Curl",
                "Drag Curl", "Zottman Curl", "Reverse Curl", "21s Curl",
                // Triceps
                "Tricep Pushdown (Cable)", "Rope Pushdown", "V-Bar Pushdown",
                "Skull Crusher (EZ Bar)", "Overhead Tricep Extension (Dumbbell)",
                "Overhead Tricep Extension (Cable)", "French Press",
                "Close-Grip Bench Press", "Diamond Push-up", "Bench Dip",
                "Weighted Bench Dip", "Machine Tricep Dip", "Tricep Kickback",
                // Forearm
                "Wrist Curl", "Reverse Wrist Curl", "Farmer's Walk",
                "Wrist Roller"
            }},
            new() { MuscleGroup = "Karın", Exercises = new() {
                "Plank", "Side Plank", "Crunch", "Reverse Crunch",
                "Leg Raise", "Hanging Leg Raise", "Ab Wheel Rollout",
                "Cable Crunch", "Machine Crunch",
                "Russian Twist", "Dead Bug", "L-Sit", "V-Up",
                "Bicycle Crunch", "Dragon Flag", "Toe Touch",
                "Flutter Kick", "Mountain Climber", "Sit-up"
            }},
            new() { MuscleGroup = "Full Body / Kardiyo", Exercises = new() {
                "Burpee", "Kettlebell Swing", "Turkish Get-up",
                "Clean and Press", "Snatch", "Clean", "Thruster",
                "Muscle-up", "Farmer's Walk", "Battle Rope",
                "Sled Push", "Sled Pull", "Jump Rope",
                "Box Jump", "Wall Ball", "Ball Slam",
                "Tire Flip", "Sledgehammer Swing"
            }},
        };
        return catalog;
    }
}
