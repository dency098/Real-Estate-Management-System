using System;
using System.Collections.Generic;

namespace RealEstateProject.Models;

public partial class Transection
{
    public int TransectionId { get; set; }

    public int PropertyId { get; set; }

    public int UserId { get; set; }

    public decimal FinalPrice { get; set; }

    public decimal CommissionAmount { get; set; }

    public decimal CommissionPercentage { get; set; }

    // ✅ FIXED (IMPORTANT)
    public DateTime TransactionDate { get; set; }


    // ✅ ADDED (matches DB)
    public string Status { get; set; } = "Completed";

    public virtual ICollection<AdminCommission> AdminCommissions { get; set; } = new List<AdminCommission>();

    public virtual Property Property { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
//using System;
//using System.Collections.Generic;

//namespace RealEstateProject.Models;

//public partial class Transection
//{
//    public int TransectionId { get; set; }

//    public int PropertyId { get; set; }

//    public int UserId { get; set; }

//    public decimal FinalPrice { get; set; }

//    public decimal CommissionAmount { get; set; }

//    public decimal CommissionPercentage { get; set; }

//    public byte[] TrasectionDate { get; set; } = null!;

//    public virtual ICollection<AdminCommission> AdminCommissions { get; set; } = new List<AdminCommission>();

//    public virtual Property Property { get; set; } = null!;

//    public virtual User User { get; set; } = null!;
//}
