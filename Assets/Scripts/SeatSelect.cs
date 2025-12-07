using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class SeatSelector : MonoBehaviour
{
    public TMP_Dropdown areaDropdown;   // 구역
    public TMP_Dropdown rowDropdown;    // 열
    public TMP_Dropdown seatDropdown;   // 번호

    // 구역 -> (열 -> 번호들)
    private Dictionary<string, Dictionary<string, List<int>>> seatData =
        new Dictionary<string, Dictionary<string, List<int>>>
    {
        {
            "A구역", new Dictionary<string, List<int>>
            {
                { "4열", new List<int>{ 1,2,3 } },
                { "15열", new List<int>{ 1,2,3 } },
                { "26열", new List<int>{ 1,2,3 } }
            }
        },
        {
            "B구역", new Dictionary<string, List<int>>
            {
                { "4열", new List<int>{ 1,2,3 } },
                { "15열", new List<int>{ 1,2,3 } },
                { "26열", new List<int>{ 1,2,3 } }
            }
        },
        {
            "D구역", new Dictionary<string, List<int>>
            {
                { "4열", new List<int>{ 1,2,3 } },
                { "15열", new List<int>{ 1,2,3 } },
                { "26열", new List<int>{ 1,2,3 } }
            }
        }
    };

    void Start()
    {
        // 드롭다운 값이 바뀔 때 호출될 함수 등록
        areaDropdown.onValueChanged.AddListener(OnAreaChanged);
        rowDropdown.onValueChanged.AddListener(OnRowChanged);

        InitAreaDropdown();
    }

    void InitAreaDropdown()
    {
        areaDropdown.ClearOptions();
        var areas = seatData.Keys.ToList();
        areaDropdown.AddOptions(areas);

        // 처음 시작할 때도 한 번 갱신
        OnAreaChanged(0);
    }

    void OnAreaChanged(int index)
    {
        string selectedArea = areaDropdown.options[index].text;

        // 선택된 구역에 맞는 열 목록 갱신
        var rows = seatData[selectedArea].Keys.ToList();
        rowDropdown.ClearOptions();
        rowDropdown.AddOptions(rows);

        // 열이 바뀌었으니 번호도 같이 갱신
        OnRowChanged(0);
    }

    void OnRowChanged(int index)
    {
        string selectedArea = areaDropdown.options[areaDropdown.value].text;
        string selectedRow = rowDropdown.options[index].text;

        var seats = seatData[selectedArea][selectedRow];

        seatDropdown.ClearOptions();
        seatDropdown.AddOptions(seats.Select(s => s.ToString()).ToList());
    }
}
