using Porsche928Diagnostics.Models;

namespace Porsche928Diagnostics.Modules;

public interface IEcuModule
{
    byte EcuAddress { get; }
    string EcuName { get; }

    Task<EcuIdentification> ReadEcuIdentificationAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DiagnosticTroubleCode>> ReadDtcsAsync(CancellationToken ct = default);
    Task ClearDtcsAsync(CancellationToken ct = default);
}
