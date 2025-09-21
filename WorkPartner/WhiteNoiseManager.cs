using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace WorkPartner
{
    // 백색 소음 재생을 관리하는 클래스
    public class WhiteNoiseManager
    {
        private Dictionary<string, MediaPlayer> players = new Dictionary<string, MediaPlayer>();

        public WhiteNoiseManager()
        {
            // 재생할 소음 종류를 초기화합니다.
            // 중요: 프로젝트에 Sounds 폴더를 만들고 wave.mp3, forest.mp3, rain.mp3, campfire.mp3 파일을 추가해야 합니다.
            // 추가된 사운드 파일의 속성에서 '빌드 작업'을 '콘텐츠'로, '출력 디렉터리로 복사'를 '항상 복사' 또는 '새 버전이면 복사'로 설정해주세요.
            InitializePlayer("Wave", "Sounds/wave.mp3");
            InitializePlayer("Forest", "Sounds/forest.mp3");
            InitializePlayer("Rain", "Sounds/rain.mp3");
            InitializePlayer("Campfire", "Sounds/campfire.mp3");
        }

        private void InitializePlayer(string name, string filePath)
        {
            try
            {
                MediaPlayer player = new MediaPlayer();
                player.Open(new Uri(filePath, UriKind.RelativeOrAbsolute));
                player.MediaEnded += (sender, e) =>
                {
                    player.Position = TimeSpan.Zero;
                    player.Play();
                };
                players[name] = player;
            }
            catch (Exception ex)
            {
                // 사운드 파일 로드 실패 시 오류를 출력합니다.
                Console.WriteLine($"Error loading sound {filePath}: {ex.Message}");
            }
        }

        // 특정 소리의 볼륨을 조절합니다.
        public void SetVolume(string soundName, double volume)
        {
            if (players.ContainsKey(soundName))
            {
                players[soundName].Volume = volume;
                if (volume > 0 && players[soundName].Source != null && (players[soundName].Position == TimeSpan.Zero || players[soundName].Position == players[soundName].NaturalDuration))
                {
                    players[soundName].Play();
                }
                else if (volume == 0)
                {
                    players[soundName].Pause(); // 볼륨이 0이면 일시정지
                }
            }
        }

        // 모든 소리를 정지합니다.
        public void StopAll()
        {
            foreach (var player in players.Values)
            {
                player.Stop();
            }
        }
    }
}
