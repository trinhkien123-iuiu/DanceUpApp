using System;

namespace DanceUp.AI
{
    /// <summary>
    /// Giao diện trừu tượng cho mô hình đánh giá chuyển động khớp.
    /// Giúp việc thay thế mô hình ONNX mới trong tương lai hoàn toàn độc lập,
    /// không cần sửa đổi UI, Quản lý phiên (Session) hay Camera.
    /// </summary>
    public interface IJointClassifierModel
    {
        string ModelName { get; }
        string Version { get; }

        /// <summary>
        /// Dự đoán trạng thái đạt (1) hoặc lỗi (0) cho N khớp.
        /// features: Ma trận [N, 3] gồm các đặc trưng: [Joint_Error, Joint_Cosine, Velocity_Error]
        /// </summary>
        int[] PredictJoints(float[,] features);

        /// <summary>
        /// Nạp mô hình từ byte array (file ONNX hoặc cấu hình nhị phân).
        /// </summary>
        bool LoadModel(byte[] modelBytes);
    }
}
