using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using RealEstateProject.Models;

namespace RealEstateProject.Services
{
    public class RentReminderBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(24);

        public RentReminderBackgroundService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<RealEstateProjectContext>();
                        var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();

                        // Find active rent deals (Stage == 6, status "Completed", property type "Rent")
                        var activeRentDeals = await context.Enquiries
                            .Include(e => e.Property)
                            .Include(e => e.SenderUser)
                            .Include(e => e.OwnerUser)
                            .Where(e => e.Stage == 6 && e.Property.PropertyType == "Rent")
                            .ToListAsync(stoppingToken);

                        var today = DateTime.Today;

                        foreach (var deal in activeRentRentals(activeRentDeals, today))
                        {
                            // Calculate the monthly due date based on when the deal was completed or CreatedAt
                            DateTime baseDate = deal.CreatedAt ?? today;
                            int dueDay = baseDate.Day;

                            // Calculate next due date (anniversary day of current or next month)
                            DateTime nextDueDate;
                            if (today.Day <= dueDay)
                            {
                                // Due this month
                                try
                                {
                                    nextDueDate = new DateTime(today.Year, today.Month, Math.Min(dueDay, DateTime.DaysInMonth(today.Year, today.Month)));
                                }
                                catch
                                {
                                    nextDueDate = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
                                }
                            }
                            else
                            {
                                // Due next month
                                var nextMonth = today.AddMonths(1);
                                try
                                {
                                    nextDueDate = new DateTime(nextMonth.Year, nextMonth.Month, Math.Min(dueDay, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month)));
                                }
                                catch
                                {
                                    nextDueDate = new DateTime(nextMonth.Year, nextMonth.Month, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month));
                                }
                            }

                            // Calculate days remaining
                            int daysRemaining = (nextDueDate - today).Days;

                            // System automatically sends reminder notification 2 days before monthly rent due date
                            if (daysRemaining == 2 && !deal.IsMonthlyRentPaid)
                            {
                                // Check if reminder already sent for this due date to prevent duplicate notifications
                                string alertMessage = $"Rent Due Reminder: Your monthly rent of ₹{(deal.PropertyPrice ?? 0):N0} for '{deal.Property?.Title}' is due on {nextDueDate:yyyy-MM-dd} (in 2 days).";
                                
                                bool reminderExists = await context.Notifications
                                    .AnyAsync(n => n.UserId == deal.SenderUserId && n.Message.Contains($"due on {nextDueDate:yyyy-MM-dd}"), stoppingToken);

                                if (!reminderExists)
                                {
                                    // 1. Save system database notification for tenant
                                    context.Notifications.Add(new Notification
                                        {
                                            UserId = deal.SenderUserId.Value,
                                            Message = alertMessage,
                                            IsRead = false,
                                            CreatedAt = DateTime.Now,
                                            EnquiryId = deal.EnquiryId
                                        });

                                    // 2. Send email notification to tenant
                                    if (deal.SenderUser != null && !string.IsNullOrEmpty(deal.SenderUser.Email))
                                    {
                                        string subject = $"Rent Due Reminder - {deal.Property?.Title}";
                                        string emailBody = $@"
                                            <h2>Monthly Rent Due Reminder</h2>
                                            <p>Hello {deal.SenderUser.FullName},</p>
                                            <p>This is an automated reminder that your monthly rent for the property <strong>{deal.Property?.Title}</strong> is due in <strong>2 days</strong>.</p>
                                            <p><strong>Rent Amount:</strong> ₹{(deal.PropertyPrice ?? 0):N0}</p>
                                            <p><strong>Rent Due Date:</strong> {nextDueDate:yyyy-MM-dd}</p>
                                            <br/>
                                            <p>Please log in to your dashboard to make your rent payment securely.</p>
                                            <p>Thank you,<br/>RealEstate Team</p>";
                                        
                                        emailService.SendEmail(deal.SenderUser.Email, subject, emailBody);
                                    }

                                    await context.SaveChangesAsync(stoppingToken);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log or handle error gracefully so service doesn't crash
                    Console.WriteLine($"Error in RentReminderBackgroundService: {ex.Message}");
                }

                // Wait 24 hours before checking again
                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        private System.Collections.Generic.IEnumerable<Enquiry> activeRentRentals(System.Collections.Generic.List<Enquiry> deals, DateTime today)
        {
            return deals.Where(d => d.SenderUserId.HasValue);
        }
    }
}
