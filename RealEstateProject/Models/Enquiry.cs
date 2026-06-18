using System;
using System.Collections.Generic;

namespace RealEstateProject.Models
{
    public partial class Enquiry
    {
        public int EnquiryId { get; set; }

        public int? PropertyId { get; set; }

        public int? SenderUserId { get; set; }   // Buyer

        public int? OwnerUserId { get; set; }    // Owner

        public string? Message { get; set; }

        public DateTime? CreatedAt { get; set; }

        public string? Status { get; set; } = "Pending";
        public bool IsAdminApproved { get; set; } = false;
        public decimal? TokenAmount { get; set; }
        public bool IsTokenPaid { get; set; } = false;

        public decimal? CommissionAmount { get; set; }
        public bool IsCommissionPaid { get; set; } = false;

        public int? Stage { get; set; } = 0;

        public bool IsBuyerPaid { get; set; } = false;

        public bool IsOwnerPaid { get; set; } = false;

        public decimal? PropertyPrice { get; set; }

        public decimal? AdminCommission { get; set; }

        // 🔥 NEW FLOW (DO NOT REMOVE OLD FIELDS)

        // Owner approval
        public bool IsApprovedByOwner { get; set; } = false;

        // Admin final approval to start deal
        public bool IsDealApprovedByAdmin { get; set; } = false;

        // Process tracking
        public bool IsMeetingDone { get; set; } = false;
        public bool IsDocumentVerified { get; set; } = false;
        public bool IsRegistered { get; set; } = false;

        // Separate payments (NEW SYSTEM)
        public bool IsTokenPaidByBuyer { get; set; } = false;
        public bool IsTokenPaidByOwner { get; set; } = false;

        public bool IsCommissionPaidByBuyer { get; set; } = false;
        public bool IsCommissionPaidByOwner { get; set; } = false;

        public string? RazorpayPaymentId { get; set; }
        public string? RazorpayOrderId { get; set; }

        // Rent Property Payment Process Fields
        public decimal? SecurityDepositAmount { get; set; }
        public bool IsSecurityDepositPaid { get; set; } = false;

        public decimal? FirstMonthRentAmount { get; set; }
        public bool IsFirstMonthRentPaid { get; set; } = false;

        public decimal? BrokerageChargesAmount { get; set; }
        public bool IsBrokeragePaid { get; set; } = false;

        public decimal? AgreementChargesAmount { get; set; }
        public bool IsAgreementChargesPaid { get; set; } = false;

        public decimal? MonthlyRentAmount { get; set; }
        public bool IsMonthlyRentPaid { get; set; } = false;
        public bool IsRentClosed { get; set; } = false;
        public bool IsCloseRentRequested { get; set; } = false;

        // Visit Scheduling Fields
        public DateTime? PreferredVisitDate { get; set; }
        public string? PreferredVisitTime { get; set; }
        public string? VisitStatus { get; set; } = "Pending";

        // Tenant Document Fields for Stage 4
        public string? DocumentPath { get; set; }
        public bool IsDocumentUploaded { get; set; } = false;
        public bool IsDocumentApprovedByAdmin { get; set; } = false;

        public virtual Property? Property { get; set; }

        // ✅ FIXED NAVIGATION
        public virtual User? SenderUser { get; set; }
        public virtual User? OwnerUser { get; set; }

        public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    }
}

