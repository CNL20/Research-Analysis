using ScholarTrend.Domain.Enums;

namespace ScholarTrend.Domain.Constants;

public static class RoleConstants
{
    public const string Admin = nameof(UserRole.Admin);
    public const string Researcher = nameof(UserRole.Researcher);
    public const string LecturerStudent = nameof(UserRole.LecturerStudent);

    public static readonly string[] All =
    [
        Admin,
        Researcher,
        LecturerStudent
    ];
}
