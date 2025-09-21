using Microsoft.ML;
using System;
using System.IO;
using WorkPartner.AI;

namespace WorkPartner
{
    /// <summary>
    /// ML.NET 모델을 사용하여 사용자 활동을 예측하는 서비스를 관리합니다.
    /// 싱글턴 패턴으로 구현되어 애플리케이션 전체에서 하나의 인스턴스만 사용됩니다.
    /// </summary>
    public class PredictionService
    {
        private readonly string logFilePath;
        private readonly string modelPath;
        private static PredictionService instance;
        private PredictionEngine<ModelInput, ModelOutput> predEngine;
        private MLContext mlContext;

        private PredictionService()
        {
            // DataManager를 통해 필요한 파일 경로를 가져옵니다.
            logFilePath = DataManager.Instance.GetLogsFilePath();
            modelPath = DataManager.Instance.GetModelFilePath();
            mlContext = new MLContext();

            // 모델 파일이 존재할 경우에만 예측 엔진을 생성합니다.
            if (File.Exists(modelPath))
            {
                LoadModel();
            }
        }

        /// <summary>
        /// PredictionService의 싱글턴 인스턴스를 가져옵니다.
        /// </summary>
        public static PredictionService Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new PredictionService();
                }
                return instance;
            }
        }

        /// <summary>
        /// 저장된 ML.NET 모델을 로드하고 예측 엔진을 생성합니다.
        /// </summary>
        private void LoadModel()
        {
            try
            {
                DataViewSchema modelSchema;
                ITransformer trainedModel = mlContext.Model.Load(modelPath, out modelSchema);
                predEngine = mlContext.Model.CreatePredictionEngine<ModelInput, ModelOutput>(trainedModel);
            }
            catch (Exception ex)
            {
                // 모델 로드 실패 시 오류를 처리할 수 있습니다. (예: 로그 기록)
                Console.WriteLine($"Error loading model: {ex.Message}");
                predEngine = null;
            }
        }

        /// <summary>
        /// 주어진 입력 데이터로 예측을 수행합니다.
        /// </summary>
        /// <param name="input">예측에 사용할 입력 데이터</param>
        /// <returns>예측 결과</returns>
        public ModelOutput Predict(ModelInput input)
        {
            if (predEngine == null)
            {
                // 모델이 로드되지 않았을 경우 기본값을 반환하거나 예외를 발생시킬 수 있습니다.
                return null;
            }
            return predEngine.Predict(input);
        }

        /// <summary>
        /// 모델을 다시 학습하고 예측 엔진을 재생성해야 할 때 사용될 수 있습니다.
        /// (현재는 외부에서 모델을 생성한다고 가정)
        /// </summary>
        public void RetrainModel()
        {
            // 여기에 모델 재학습 로직을 구현할 수 있습니다.
            // 예를 들어, 새로운 로그 데이터로 모델을 다시 학습하고 저장한 뒤,
            // LoadModel()을 호출하여 예측 엔진을 업데이트합니다.
            if (File.Exists(modelPath))
            {
                LoadModel();
            }
        }
    }
}

