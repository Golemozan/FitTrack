using System.Text.Json.Serialization;
namespace FitTrack.API.Models;

public enum MealType
{
    Breakfast,
    Lunch,
    Dinner,
    Snack
}

public class MealEntry : IUserOwned
{
    [JsonIgnore]
    public Guid UserId { get; set; }

    public Guid Id { get; set; }
    public string FoodName { get; set; } = string.Empty;
    public double Grams { get; set; }
    public double Calories { get; set; }
    public double Protein { get; set; }
    public double Carbs { get; set; }
    public double Fat { get; set; }
    public MealType MealType { get; set; }
    public DateTime LoggedAt { get; set; }
}
