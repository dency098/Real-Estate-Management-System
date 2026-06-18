using System;
using System.Collections.Generic;

namespace RealEstateProject.Models;

public partial class AdminCommission
{
    public int CommissionId { get; set; }

    public int TransectionId { get; set; }

    public decimal Amount { get; set; }

    public string Status { get; set; } = "Pending";  // better default

    // ✅ ADD THIS (IMPORTANT)
    public DateTime? PaidDate { get; set; }

    public virtual Transection Transection { get; set; } = null!;
}


//using System;
//using System.Collections.Generic;

//namespace RealEstateProject.Models;

//public partial class AdminCommission
//{
//    public int CommissionId { get; set; }

//    public int TransectionId { get; set; }

//    public decimal Amount { get; set; }

//    public string Status { get; set; } = null!;

//    public virtual Transection Transection { get; set; } = null!;
//}
