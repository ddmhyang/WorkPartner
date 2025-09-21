using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using WorkPartner;

public class AppSettings
{
    // 사용자 프로필 정보
    public string Nickname { get; set; } = "WorkPartner";
    public long Experience { get; set; } = 0;
    public int Coins { get; set; } = 1000;

    // 타이머 및 추적 설정
    public bool IsIdleDetectionEnabled { get; set; } = true;
    public int IdleTimeoutSeconds { get; set; } = 300;
    public bool IsMiniTimerEnabled { get; set; } = false;

    // 집중 모드 설정
    public string FocusModeNagMessage { get; set; } = "딴짓 그만! 다시 집중해주세요!";
    public int FocusModeNagIntervalSeconds { get; set; } = 60;

    // 추적할 프로세스 및 URL 목록
    public List<string> WorkProcesses { get; set; } = new List<string> { "visual studio", "devenv", "code", "word", "excel", "powerpnt", "hwp" };
    public List<string> PassiveProcesses { get; set; } = new List<string> { "vlc", "potplayer", "youtubemusic" };
    public List<string> DistractionProcesses { get; set; } = new List<string> { "league of legends", "steam", "netflix", "disneyplus" };

    // AI 태그 추천 규칙
    public Dictionary<string, string> TagRules { get; set; } = new Dictionary<string, string>();

    // 아바타 꾸미기 관련
    public List<Guid> OwnedItemIds { get; set; } = new List<Guid>();
    public Dictionary<ItemType, Guid> EquippedItems { get; set; } = new Dictionary<ItemType, Guid>();
    public Dictionary<ItemType, string> CustomColors { get; set; } = new Dictionary<ItemType, string>();

    // 과목별 색상
    public Dictionary<string, string> TaskColors { get; set; } = new Dictionary<string, string>();
}

