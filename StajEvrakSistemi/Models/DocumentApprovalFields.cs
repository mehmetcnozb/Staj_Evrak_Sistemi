namespace StajEvrakSistemi.Models;

// Null, alanin henuz incelenmedigini belirtir. Bos alan ile ayni anlama gelmez.
public sealed record DocumentApprovalFields(
    bool? StudentApprovalPresent = null,
    bool? AdvisorApprovalPresent = null,
    bool? EmployerFieldsComplete = null,
    bool? EmployerStampPresent = null,
    bool? EmployerSignaturePresent = null);
