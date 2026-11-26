namespace SmartHome.Hub.UI;

/// <summary>
/// 상태에 따라 코드에서 바꾸는 색 (켜짐/꺼짐, 연결/오프라인, 성공/실패).
/// 화면 디자인을 바꿀 때 이 색만 고치면 전체에 반영돼요.
/// 글꼴, 배치, 배경색 같은 고정된 모양은 디자이너(MainForm.cs 더블클릭)에서 바꾸면 돼요.
/// </summary>
internal static class Theme
{
    public static readonly Color On = Color.SeaGreen;          // 켜짐, 연결됨, 성공
    public static readonly Color Off = Color.DimGray;          // 꺼짐, 보통 글자
    public static readonly Color Bad = Color.Firebrick;        // 오프라인, 실패
    public static readonly Color Warn = Color.DarkOrange;      // 연결은 됐지만 Arduino 응답 없음
    public static readonly string ChartFont = "Malgun Gothic"; // 그래프 글꼴 (한글이 나오는 글꼴이어야 해요)
}
