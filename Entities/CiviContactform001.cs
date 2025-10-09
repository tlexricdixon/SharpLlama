using System;
using System.Collections.Generic;

namespace SharpLlama.Entities;

public partial class CiviContactform001
{
    public int Id { get; set; }

    public string FirstName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string? Message { get; set; }
}
