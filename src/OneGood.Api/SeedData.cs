using OneGood.Core.Enums;
using OneGood.Core.Models;
using OneGood.Infrastructure.Data;

namespace OneGood.Api;

/// <summary>
/// Seeds sample data for development and demo purposes.
/// In production, causes would come from external APIs.
/// </summary>
public static class SeedData
{
    public static async Task SeedAsync(OneGoodDbContext db)
    {
        if (db.Causes.Any()) return;

        var causes = new List<Cause>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Title = "Solar-powered water pump for Kenyan village",
                Summary = "347 villagers in rural Kenya are €127 away from clean water. Your donation completes their solar water pump.",
                Description = "A small village in rural Kenya needs just €127 more to complete their solar water pump installation. 347 people will have clean water for the first time. The project has been running for 6 months and has already installed the solar panels and well infrastructure. This final push will complete the pump mechanism and filtration system, providing sustainable clean water for generations.",
                Category = CauseCategory.CleanWater,
                OrganisationName = "Water.org",
                OrganisationUrl = "https://water.org",
                FundingGoal = 2500,
                FundingCurrent = 2373,
                Deadline = DateTime.UtcNow.AddDays(3),
                UrgencyScore = 92,
                LeverageScore = 88,
                ActionsToTippingPoint = 25,
                SourceApiName = "Seed",
                SourceExternalId = "seed-001",
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                Title = "Reforestation in the Amazon",
                Summary = "Plant trees in the Amazon. €5 plants 10 native trees that will absorb carbon for decades.",
                Description = "Plant native trees to restore 10 hectares of deforested Amazon rainforest. Each €5 plants 10 trees that will absorb carbon for decades. This project works with local indigenous communities to plant and maintain native tree species, providing both environmental benefits and economic opportunities for local families.",
                Category = CauseCategory.ClimateAndNature,
                OrganisationName = "One Tree Planted",
                OrganisationUrl = "https://onetreeplanted.org",
                FundingGoal = 5000,
                FundingCurrent = 4200,
                Deadline = DateTime.UtcNow.AddDays(14),
                UrgencyScore = 75,
                LeverageScore = 70,
                ActionsToTippingPoint = 160,
                SourceApiName = "Seed",
                SourceExternalId = "seed-002",
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                Title = "School supplies for refugee children in Lebanon",
                Summary = "50 Syrian refugee children need school supplies. €3 gives one child a full kit to start the new term.",
                Description = "50 Syrian refugee children in Beirut need school supplies to start the new term. €3 provides a full kit for one child including notebooks, pens, pencils, and a backpack. These children have fled conflict and deserve a chance at education. The school term starts in just 5 days.",
                Category = CauseCategory.Refugees,
                OrganisationName = "UNHCR",
                OrganisationUrl = "https://unhcr.org",
                FundingGoal = 150,
                FundingCurrent = 102,
                Deadline = DateTime.UtcNow.AddDays(5),
                UrgencyScore = 85,
                LeverageScore = 95,
                ActionsToTippingPoint = 16,
                SourceApiName = "Seed",
                SourceExternalId = "seed-003",
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                Title = "Emergency food aid for Yemen",
                Summary = "Families in Yemen face acute hunger. €10 feeds a family for a week.",
                Description = "Families in Yemen face acute food insecurity due to ongoing conflict. €10 provides a family with a week of essential food supplies including rice, lentils, oil, and flour. The World Food Programme is working to reach the most vulnerable families before conditions worsen further.",
                Category = CauseCategory.FoodSecurity,
                OrganisationName = "World Food Programme",
                OrganisationUrl = "https://wfp.org",
                FundingGoal = 10000,
                FundingCurrent = 7500,
                Deadline = DateTime.UtcNow.AddDays(7),
                UrgencyScore = 80,
                LeverageScore = 65,
                ActionsToTippingPoint = 250,
                SourceApiName = "Seed",
                SourceExternalId = "seed-004",
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                Title = "Mental health helpline for teens",
                Summary = "Fund 24/7 mental health support for teens in crisis. €2 = one hour of counselor time.",
                Description = "Fund 24/7 mental health support for young people in crisis. Each €2 funds one hour of trained counselor time. The Crisis Text Line connects teens with trained counselors via text message, providing immediate support when they need it most. Last year they helped over 50,000 young people through difficult moments.",
                Category = CauseCategory.MentalHealth,
                OrganisationName = "Crisis Text Line",
                OrganisationUrl = "https://crisistextline.org",
                FundingGoal = 3000,
                FundingCurrent = 2100,
                Deadline = DateTime.UtcNow.AddDays(10),
                UrgencyScore = 70,
                LeverageScore = 80,
                ActionsToTippingPoint = 450,
                SourceApiName = "Seed",
                SourceExternalId = "seed-005",
                IsActive = true
            }
        };

        db.Causes.AddRange(causes);
        await db.SaveChangesAsync();
    }
}
