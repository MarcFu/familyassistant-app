using FamilyAssistant.Models;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistant.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Person> Persons => Set<Person>();
    public DbSet<PersonDevice> PersonDevices => Set<PersonDevice>();
    public DbSet<Chore> Chores => Set<Chore>();
    public DbSet<ChoreSchedule> ChoreSchedules => Set<ChoreSchedule>();
    public DbSet<ChoreTask> ChoreTasks => Set<ChoreTask>();
    public DbSet<InternetRule> InternetRules => Set<InternetRule>();
    public DbSet<CreditTransaction> CreditTransactions => Set<CreditTransaction>();
    public DbSet<MissedCreditsLog> MissedCreditsLogs => Set<MissedCreditsLog>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<TaskComment> TaskComments => Set<TaskComment>();
    public DbSet<TaskAttachment> TaskAttachments => Set<TaskAttachment>();
    public DbSet<EventTrigger> EventTriggers => Set<EventTrigger>();
    public DbSet<PersonAchievement> PersonAchievements => Set<PersonAchievement>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeCategory> RecipeCategories => Set<RecipeCategory>();
    public DbSet<RecipeTag> RecipeTags => Set<RecipeTag>();
    public DbSet<RecipeIngredientGroup> RecipeIngredientGroups => Set<RecipeIngredientGroup>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<RecipeStep> RecipeSteps => Set<RecipeStep>();
    public DbSet<RecipeStepIngredient> RecipeStepIngredients => Set<RecipeStepIngredient>();
    public DbSet<RecipeImage> RecipeImages => Set<RecipeImage>();
    public DbSet<RecipeStepImage> RecipeStepImages => Set<RecipeStepImage>();
    public DbSet<RecipeImportCandidate> RecipeImportCandidates => Set<RecipeImportCandidate>();
    public DbSet<RecipeImportTraceEntry> RecipeImportTraceEntries => Set<RecipeImportTraceEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // --- AppSetting (key-value store) ---
        modelBuilder.Entity<AppSetting>(entity =>
        {
            entity.HasKey(s => s.Key);
        });

        // --- Person ---
        modelBuilder.Entity<Person>(entity =>
        {
            entity.HasIndex(p => p.HaEntityId).IsUnique();
        });

        // --- PersonDevice ---
        modelBuilder.Entity<PersonDevice>(entity =>
        {
            entity.HasOne(pd => pd.Person)
                .WithMany(p => p.Devices)
                .HasForeignKey(pd => pd.PersonId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // --- ChoreSchedule ---
        modelBuilder.Entity<ChoreSchedule>(entity =>
        {
            entity.HasOne(cs => cs.Chore)
                .WithMany(c => c.Schedules)
                .HasForeignKey(cs => cs.ChoreId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(cs => cs.DefaultPerson)
                .WithMany(p => p.DefaultSchedules)
                .HasForeignKey(cs => cs.DefaultPersonId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // --- ChoreTask ---
        modelBuilder.Entity<ChoreTask>(entity =>
        {
            entity.HasOne(ct => ct.Chore)
                .WithMany(c => c.Tasks)
                .HasForeignKey(ct => ct.ChoreId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ct => ct.Schedule)
                .WithMany()
                .HasForeignKey(ct => ct.ScheduleId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired(false);

            entity.HasOne(ct => ct.DefaultPerson)
                .WithMany()
                .HasForeignKey(ct => ct.DefaultPersonId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(ct => ct.ClaimedByPerson)
                .WithMany(p => p.ClaimedTasks)
                .HasForeignKey(ct => ct.ClaimedByPersonId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(ct => ct.CompletedByPerson)
                .WithMany()
                .HasForeignKey(ct => ct.CompletedByPersonId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(ct => ct.ConfirmedByPerson)
                .WithMany()
                .HasForeignKey(ct => ct.ConfirmedByPersonId)
                .OnDelete(DeleteBehavior.SetNull);

            // Index for efficient querying of open tasks
            entity.HasIndex(ct => new { ct.Status, ct.DueDate });
            // Index for TaskGenerator idempotency checks (not unique — enforced in code)
            entity.HasIndex(ct => new { ct.ScheduleId, ct.DueDate, ct.OccurrenceIndex });
        });

        // --- InternetRule ---
        modelBuilder.Entity<InternetRule>(entity =>
        {
            entity.HasOne(ir => ir.Person)
                .WithMany(p => p.InternetRules)
                .HasForeignKey(ir => ir.PersonId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(ir => ir.AffectedDevices)
                .WithMany(pd => pd.InternetRules)
                .UsingEntity("InternetRuleDevices");
        });

        // --- CreditTransaction ---
        modelBuilder.Entity<CreditTransaction>(entity =>
        {
            entity.HasOne(ct => ct.Person)
                .WithMany(p => p.CreditTransactions)
                .HasForeignKey(ct => ct.PersonId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ct => ct.ChoreTask)
                .WithMany()
                .HasForeignKey(ct => ct.ChoreTaskId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(ct => new { ct.PersonId, ct.Timestamp });
        });

        // --- MissedCreditsLog ---
        modelBuilder.Entity<MissedCreditsLog>(entity =>
        {
            entity.HasOne(m => m.Person)
                .WithMany()
                .HasForeignKey(m => m.PersonId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(m => m.ChoreTask)
                .WithMany()
                .HasForeignKey(m => m.ChoreTaskId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(m => m.Schedule)
                .WithMany()
                .HasForeignKey(m => m.ScheduleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(m => new { m.PersonId, m.Date });
        });

        // --- TaskComment ---
        modelBuilder.Entity<TaskComment>(entity =>
        {
            entity.HasOne(tc => tc.ChoreTask)
                .WithMany(ct => ct.Comments)
                .HasForeignKey(tc => tc.ChoreTaskId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(tc => tc.Person)
                .WithMany()
                .HasForeignKey(tc => tc.PersonId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(tc => new { tc.ChoreTaskId, tc.CreatedAt });
        });

        // --- TaskAttachment ---
        modelBuilder.Entity<TaskAttachment>(entity =>
        {
            entity.HasOne(ta => ta.TaskComment)
                .WithMany(tc => tc.Attachments)
                .HasForeignKey(ta => ta.TaskCommentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // --- EventTrigger ---
        modelBuilder.Entity<EventTrigger>(entity =>
        {
            entity.HasOne(et => et.Chore)
                .WithMany()
                .HasForeignKey(et => et.ChoreId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(et => et.EntityId);
        });

        // --- PersonAchievement ---
        modelBuilder.Entity<PersonAchievement>(entity =>
        {
            entity.HasOne(pa => pa.Person)
                .WithMany()
                .HasForeignKey(pa => pa.PersonId)
                .OnDelete(DeleteBehavior.Cascade);

            // Each person can unlock each achievement only once
            entity.HasIndex(pa => new { pa.PersonId, pa.AchievementKey }).IsUnique();
        });

        // --- RecipeCategory ---
        modelBuilder.Entity<RecipeCategory>(entity =>
        {
            entity.HasOne(c => c.ParentCategory)
                .WithMany(c => c.Children)
                .HasForeignKey(c => c.ParentCategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(c => new { c.ParentCategoryId, c.Name });
        });

        // --- RecipeTag ---
        modelBuilder.Entity<RecipeTag>(entity =>
        {
            entity.HasIndex(t => t.Name).IsUnique();
        });

        // --- Recipe ---
        modelBuilder.Entity<Recipe>(entity =>
        {
            entity.HasOne(r => r.Category)
                .WithMany(c => c.Recipes)
                .HasForeignKey(r => r.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(r => r.Tags)
                .WithMany(t => t.Recipes)
                .UsingEntity("RecipeRecipeTags");

            entity.HasIndex(r => r.Name);
        });

        // --- RecipeImportCandidate ---
        modelBuilder.Entity<RecipeImportCandidate>(entity =>
        {
            entity.HasIndex(j => new { j.Status, j.CreatedAt });
            entity.HasIndex(j => j.UpdatedAt);
            entity.HasIndex(j => j.HeartbeatAt);
        });

        // --- RecipeImportTraceEntry ---
        modelBuilder.Entity<RecipeImportTraceEntry>(entity =>
        {
            entity.HasOne(e => e.Candidate)
                .WithMany(c => c.TraceEntries)
                .HasForeignKey(e => e.CandidateId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.CandidateId, e.AttemptNumber, e.SortOrder });
        });

        // --- RecipeIngredient ---
        modelBuilder.Entity<RecipeIngredientGroup>(entity =>
        {
            entity.HasOne(g => g.Recipe)
                .WithMany(r => r.IngredientGroups)
                .HasForeignKey(g => g.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(g => new { g.RecipeId, g.SortOrder });
        });

        modelBuilder.Entity<RecipeIngredient>(entity =>
        {
            entity.HasOne(i => i.Recipe)
                .WithMany(r => r.Ingredients)
                .HasForeignKey(i => i.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(i => i.Group)
                .WithMany(g => g.Ingredients)
                .HasForeignKey(i => i.GroupId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(i => new { i.RecipeId, i.SortOrder });
            entity.HasIndex(i => new { i.RecipeId, i.MarkerToken });
        });

        // --- RecipeStep ---
        modelBuilder.Entity<RecipeStep>(entity =>
        {
            entity.HasOne(s => s.Recipe)
                .WithMany(r => r.Steps)
                .HasForeignKey(s => s.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(s => new { s.RecipeId, s.SortOrder });
        });

        // --- RecipeStepImage ---
        modelBuilder.Entity<RecipeStepImage>(entity =>
        {
            entity.HasOne(i => i.RecipeStep)
                .WithMany(s => s.Images)
                .HasForeignKey(i => i.RecipeStepId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(i => new { i.RecipeStepId, i.CreatedAt });
        });

        // --- RecipeStepIngredient ---
        modelBuilder.Entity<RecipeStepIngredient>(entity =>
        {
            entity.HasKey(link => new { link.RecipeStepId, link.RecipeIngredientId });

            entity.HasOne(link => link.RecipeStep)
                .WithMany(step => step.Ingredients)
                .HasForeignKey(link => link.RecipeStepId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(link => link.RecipeIngredient)
                .WithMany(ingredient => ingredient.StepLinks)
                .HasForeignKey(link => link.RecipeIngredientId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // --- RecipeImage ---
        modelBuilder.Entity<RecipeImage>(entity =>
        {
            entity.HasOne(i => i.Recipe)
                .WithMany(r => r.Images)
                .HasForeignKey(i => i.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(i => new { i.RecipeId, i.CreatedAt });
        });
    }
}
