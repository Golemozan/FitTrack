// Mirrors FitTrack.API models & DTOs. JSON is camelCase; enums serialize as strings.

export type MealType = "Breakfast" | "Lunch" | "Dinner" | "Snack";

export const MEAL_TYPES: MealType[] = ["Breakfast", "Lunch", "Dinner", "Snack"];

// ---- Entities ----

export interface MealEntry {
  id: string;
  foodName: string;
  grams: number;
  calories: number;
  protein: number;
  carbs: number;
  fat: number;
  mealType: MealType;
  loggedAt: string;
}

export interface ExerciseSet {
  id: string;
  exerciseId: string;
  setNumber: number;
  weightKg: number;
  reps: number;
  isCompleted: boolean;
}

export interface Exercise {
  id: string;
  workoutSessionId: string;
  name: string;
  muscleGroup: string;
  sets: ExerciseSet[];
}

export interface WorkoutSession {
  id: string;
  name: string;
  loggedAt: string;
  exercises: Exercise[];
}

export interface WeightLog {
  id: string;
  weightKg: number;
  loggedAt: string;
  notes: string | null;
}

export interface UserGoals {
  id: string;
  calorieGoal: number;
  proteinGoal: number;
  carbGoal: number;
  fatGoal: number;
  updatedAt: string;
}

// ---- Nutrition DTOs ----

export interface NutritionResult {
  foodName: string;
  calories: number;
  protein: number;
  carbs: number;
  fat: number;
  servingGrams: number;
}

export interface LogFoodRequest {
  foodName: string;
  grams: number;
  calories: number;
  protein: number;
  carbs: number;
  fat: number;
  mealType: MealType;
  logDate?: string; // YYYY-MM-DD — geçmiş güne ekleme
}

export interface NutritionSummary {
  totalCalories: number;
  totalProtein: number;
  totalCarbs: number;
  totalFat: number;
}

/** /api/nutrition/today → entries keyed by meal type. */
export type TodayMeals = Partial<Record<MealType, MealEntry[]>>;

// ---- Nutrition History ----

export interface DailyNutritionSummary {
  date: string;
  totalCalories: number;
  totalProtein: number;
  totalCarbs: number;
  totalFat: number;
  mealCount: number;
}

export interface NutritionStreak {
  days: number;
}

// ---- Goals DTO ----

export interface UpdateGoalsRequest {
  calorieGoal: number;
  proteinGoal: number;
  carbGoal: number;
  fatGoal: number;
}

// ---- Workout DTOs ----

export interface CreateSessionRequest {
  name: string;
}

export interface AddExerciseRequest {
  workoutSessionId: string;
  name: string;
  muscleGroup: string;
}

export interface UpdateExerciseRequest {
  name: string;
  muscleGroup: string;
}

export interface AddSetRequest {
  exerciseId: string;
  setNumber: number;
  weightKg: number;
  reps: number;
  isCompleted: boolean;
}

export interface UpdateSetRequest {
  weightKg: number;
  reps: number;
  isCompleted: boolean;
}

export interface SessionSummary {
  id: string;
  name: string;
  loggedAt: string;
  exerciseCount: number;
}

export interface ExerciseHistoryItem {
  sessionId: string;
  sessionName: string;
  loggedAt: string;
  muscleGroup: string;
  sets: ExerciseSet[];
}

export interface MuscleGroupExercises {
  muscleGroup: string;
  exercises: string[];
}

// ---- Check-in ----

export interface CheckIn {
  id: string;
  mood: number;    // 1-5
  energy: number;  // 1-5
  hunger: number;  // 1-5
  note: string | null;
  context: string | null; // "general" | "pre-workout" | "post-workout"
  loggedAt: string;
}

export interface LogCheckInRequest {
  mood: number;
  energy: number;
  hunger: number;
  note: string | null;
  context: string;
}

// ---- Profile ----

export interface Profile {
  id: string;
  heightCm: number | null;
  targetWeightKg: number | null;
  updatedAt: string;
}

export interface UpdateProfileRequest {
  heightCm: number | null;
  targetWeightKg: number | null;
}

// ---- Coach (AI) ----

export interface CoachMessage {
  role: "user" | "assistant";
  content: string;
}

export interface CoachChatResponse {
  reply: string;
  actions: string[]; // domains coach mutated: "nutrition" | "weight" | "checkin"
}

// ---- Weight DTOs ----

export interface LogWeightRequest {
  weightKg: number;
  notes: string | null;
  loggedAt?: string; // ISO date string — optional, defaults to today
}

export interface WeightPoint {
  loggedAt: string;
  weightKg: number;
}

export interface WeightStats {
  currentWeight: number | null;
  startWeight: number | null;
  lowestWeight: number | null;
  highestWeight: number | null;
  totalChange: number | null;
  weeklyChange: number | null;
  lastLoggedAt: string | null;
}

// ---- Alışkanlıklar (2026-11-01) ----

export type HabitIcon = "water" | "pill" | "steps" | "stretch" | "sleep" | "book" | "apple" | "check";

export interface Habit {
  id: string;
  name: string;
  icon: HabitIcon;
  target: number;
  unit: string | null;
  sortOrder: number;
  count: number;
}

export interface HabitRequest {
  name: string;
  icon: HabitIcon;
  target: number;
  unit: string | null;
}

// ---- Antrenman şablonları ----

export interface TemplateExercise {
  id: string;
  name: string;
  muscleGroup: string;
  sortOrder: number;
  setCount: number;
  targetReps: number;
}

export interface WorkoutTemplate {
  id: string;
  name: string;
  createdAt: string;
  lastUsedAt: string | null;
  exercises: TemplateExercise[];
}

// ---- Haftalık özet ----

export interface PeriodStats {
  from: string;
  to: string;
  mealDays: number;
  avgCalories: number;
  avgProtein: number;
  calorieGoalDays: number;
  proteinGoalDays: number;
  workouts: number;
  completedSets: number;
  volumeKg: number;
  weightStart: number | null;
  weightEnd: number | null;
  weightChange: number | null;
  avgMood: number | null;
  avgEnergy: number | null;
  checkIns: number;
  habitRate: number | null;
  avgWater: number | null;
}

export interface WeeklySummary {
  thisWeek: PeriodStats;
  previousWeek: PeriodStats;
  calorieGoal: number;
  proteinGoal: number;
  days: Array<{ date: string; calories: number; meals: number; workout: boolean; mood: number | null }>;
  habits: Array<{ id: string; name: string; icon: HabitIcon; target: number; unit: string | null; daysDone: number; counts: number[] }>;
  highlights: string[];
}

// ---- Başarımlar ----

export type AchievementCategory = "nutrition" | "workout" | "weight" | "mind" | "general";

export interface Achievement {
  key: string;
  category: AchievementCategory;
  title: string;
  description: string;
  icon: string;
  unit: string;
  value: number;
  level: number;
  maxLevel: number;
  nextTarget: number | null;
  isNew: boolean;
  hint: string | null;
  tiers: Array<{ level: number; target: number; unlockedAt: string | null }>;
}

// ---- Ana sayfa kart düzeni ----

export type HomeWidgetType = "mood" | "habits" | "tiles" | "coach" | "tip" | "note";

export interface HomeWidget {
  id: string;
  type: HomeWidgetType;
  text?: string | null;
  topics?: string[] | null;
}
