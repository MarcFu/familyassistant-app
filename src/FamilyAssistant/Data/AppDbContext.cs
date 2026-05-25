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
    }
}
