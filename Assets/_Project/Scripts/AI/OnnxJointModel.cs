using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace DanceUp.AI
{
    /// <summary>
    /// Bộ thực thi mô hình ONNX chính quy sử dụng Microsoft.ML.OnnxRuntime.
    /// Hoàn toàn độc lập với loại mô hình huấn luyện (CatBoost, XGBoost, PyTorch, v.v.).
    /// Hỗ trợ thay thế file .onnx mới mà không cần sửa đổi bất kỳ code UI hoặc logic nào.
    /// </summary>
    public class OnnxJointModel : IJointClassifierModel, IDisposable
    {
        public string ModelName => "ONNX_Joint_Classifier";
        public string Version => "1.0-onnxruntime";

        private InferenceSession session;
        private CatBoostJointModel fallbackModel;
        private bool isSessionValid = false;

        public OnnxJointModel(string onnxFilePath = null)
        {
            fallbackModel = new CatBoostJointModel();

            if (!string.IsNullOrEmpty(onnxFilePath) && File.Exists(onnxFilePath))
            {
                LoadFromFile(onnxFilePath);
            }
            else
            {
                string defaultPath = Path.Combine(Application.streamingAssetsPath, "joint_classifier.onnx");
                if (File.Exists(defaultPath))
                {
                    LoadFromFile(defaultPath);
                }
            }
        }

        public bool LoadFromFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    byte[] bytes = File.ReadAllBytes(filePath);
                    return LoadModel(bytes);
                }
                Debug.LogWarning("[OnnxJointModel] Không tìm thấy file ONNX tại: " + filePath);
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError("[OnnxJointModel] Lỗi đọc file ONNX: " + ex.Message);
                return false;
            }
        }

        public bool LoadModel(byte[] modelBytes)
        {
            if (modelBytes == null || modelBytes.Length == 0)
            {
                Debug.LogWarning("[OnnxJointModel] Dữ liệu model bytes rỗng!");
                return false;
            }

            try
            {
                DisposeSession();
                var options = new SessionOptions();
                session = new InferenceSession(modelBytes, options);
                isSessionValid = true;
                Debug.Log("[OnnxJointModel] Nạp thành công mô hình ONNX (" + modelBytes.Length.ToString("N0") + " bytes) qua ONNX Runtime.");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError("[OnnxJointModel] Lỗi khởi tạo InferenceSession ONNX: " + ex.Message + ". Sẽ sử dụng Fallback Evaluator.");
                isSessionValid = false;
                fallbackModel.LoadModel(modelBytes);
                return false;
            }
        }

        public int[] PredictJoints(float[,] features)
        {
            if (features == null) return new int[0];
            int n = features.GetLength(0);
            int m = features.GetLength(1);

            if (n == 0 || m == 0) return new int[0];

            if (isSessionValid && session != null)
            {
                try
                {
                    var dimensions = new int[] { n, m };
                    var tensor = new DenseTensor<float>(dimensions);

                    for (int i = 0; i < n; i++)
                    {
                        for (int j = 0; j < m; j++)
                        {
                            tensor[i, j] = features[i, j];
                        }
                    }

                    var inputs = new List<NamedOnnxValue>
                    {
                        NamedOnnxValue.CreateFromTensor("features", tensor)
                    };

                    using (var results = session.Run(inputs))
                    {
                        foreach (var output in results)
                        {
                            if (output.Name == "label")
                            {
                                var labelTensor = output.AsTensor<long>();
                                int[] predictions = new int[n];
                                int idx = 0;
                                foreach (var val in labelTensor)
                                {
                                    if (idx < n)
                                    {
                                        // 1 = Pass (khớp chuẩn), 0 = Fail (khớp sai) theo chuẩn processing_11
                                        predictions[idx++] = (int)val;
                                    }
                                }
                                return predictions;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[OnnxJointModel] Lỗi suy luận ONNX: " + ex.Message + ". Chuyển sang Fallback Model.");
                }
            }

            return fallbackModel.PredictJoints(features);
        }

        private void DisposeSession()
        {
            if (session != null)
            {
                session.Dispose();
                session = null;
            }
            isSessionValid = false;
        }

        public void Dispose()
        {
            DisposeSession();
        }
    }
}
