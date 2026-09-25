using Cashier.Domain;
using Shared.Kernel;

namespace Cashier.Application;

public sealed class OpenShiftHandler(IShiftRepository shifts, TimeProvider clock)
{
    public async Task<Shift> HandleAsync(string openedBy, decimal openingFloat, CancellationToken ct)
    {
        var shift = Shift.Open(await shifts.CurrentAsync(ct), openedBy, openingFloat, clock.GetUtcNow());
        shifts.Add(shift);

        // Hai người bấm "Mở ca" cùng lúc đều qua được bước kiểm tra trên; unique index quyết người thắng.
        if (!await shifts.TrySaveAsync(ct))
        {
            throw new DomainException(Shift.AlreadyOpenMessage);
        }

        return shift;
    }
}
