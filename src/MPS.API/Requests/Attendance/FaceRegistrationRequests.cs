namespace MPS.API.Requests.Attendance;

public sealed record SubmitFaceRegistrationRequest(string PhotoBase64, string EmbeddingJson);

public sealed record RejectFaceRegistrationRequest(string Reason);

public sealed record FaceCheckinRequest(
    string EmployeeId,
    DateTime CheckinTime,
    float Confidence,
    int CameraId,
    string Source);
