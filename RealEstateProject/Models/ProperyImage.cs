using System;
using System.Collections.Generic;

namespace RealEstateProject.Models;

public partial class ProperyImage
{
    public int ImageId { get; set; }

    public int PropertyId { get; set; }

    public string ImagePath { get; set; } = null!;

    public virtual Property Property { get; set; } = null!;
}
