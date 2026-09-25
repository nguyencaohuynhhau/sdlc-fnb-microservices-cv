using Cashier.Domain;
using Shared.Kernel;

namespace Cashier.Application;

public sealed class CloseShiftHandler(IShiftRepository shifts, TimeProvider clock)
{
    public async Task<Shift> HandleAsync(Guid id, CancellationToken ct)
    {
        var shift = await shifts.FindAsync(id, ct) ?? throw new NotFoundException("Không tìm thấy ca.");
        shift.Close(clock.GetUtcNow());

        // Người khác vừa đóng đúng ca này (xmin đổi) — kết quả với người dùng là như nhau.
        if (!await shifts.TrySaveAsync(ct))
        {
            throw new DomainException(Shift.AlreadyClosedMessage);
        }

        return shift;
    }
}
