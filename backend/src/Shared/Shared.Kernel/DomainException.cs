namespace Shared.Kernel;

/// <summary>
/// Vi phạm quy tắc nghiệp vụ. <see cref="Exception.Message"/> là câu tiếng Việt hiển thị
/// thẳng cho người dùng (ProblemDetails.detail), nên viết như nói với thu ngân.
/// Tầng web ánh xạ sang 409.
/// </summary>
public class DomainException(string message) : Exception(message);

/// <summary>Không tìm thấy tài nguyên — tầng web ánh xạ sang 404.</summary>
public sealed class NotFoundException(string message) : DomainException(message);

/// <summary>Yêu cầu tham chiếu thứ không tồn tại/không hợp lệ (vd. món không có trong thực đơn) — tầng web ánh xạ sang 400.</summary>
public sealed class InvalidRequestException(string message) : DomainException(message);
