using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace OneTaskBar
{
    internal static class CalendarHolidays
    {
        enum Substitute { None, Sunday, Weekend }

        sealed class Holiday
        {
            internal readonly DateTime First, Last;
            internal readonly string Name;
            internal readonly Substitute Rule;

            internal Holiday(DateTime date, string name, Substitute rule, bool threeDays = false)
            {
                First = threeDays ? date.AddDays(-1) : date;
                Last = threeDays ? date.AddDays(1) : date;
                Name = name;
                Rule = rule;
            }
        }

        internal static Dictionary<DateTime, string> ForYear(int year, string region)
        {
            var dates = new Dictionary<DateTime, string>();
            if (region != "KR") return dates;

            // 관공서의 공휴일에 관한 규정 제2·3조. Election days can be added once announced.
            var holidays = new List<Holiday>
            {
                new Holiday(new DateTime(year, 1, 1), "신정", Substitute.None),
                new Holiday(new DateTime(year, 3, 1), "삼일절", year >= 2021 ? Substitute.Weekend : Substitute.None),
                new Holiday(new DateTime(year, 5, 5), "어린이날", year >= 2014 ? Substitute.Weekend : Substitute.None),
                new Holiday(new DateTime(year, 6, 6), "현충일", Substitute.None),
                new Holiday(new DateTime(year, 8, 15), "광복절", year >= 2021 ? Substitute.Weekend : Substitute.None),
                new Holiday(new DateTime(year, 10, 3), "개천절", year >= 2021 ? Substitute.Weekend : Substitute.None),
                new Holiday(new DateTime(year, 12, 25), "성탄절", year >= 2023 ? Substitute.Weekend : Substitute.None)
            };
            if (year >= 2013) holidays.Add(new Holiday(new DateTime(year, 10, 9), "한글날", year >= 2021 ? Substitute.Weekend : Substitute.None));
            if (year >= 2026)
            {
                holidays.Add(new Holiday(new DateTime(year, 5, 1), "노동절", Substitute.Weekend));
                holidays.Add(new Holiday(new DateTime(year, 7, 17), "제헌절", Substitute.Weekend));
            }
            if (year == 2026) holidays.Add(new Holiday(new DateTime(year, 6, 3), "전국동시지방선거", Substitute.None));

            var lunar = new KoreanLunisolarCalendar();
            if (year >= lunar.GetYear(lunar.MinSupportedDateTime) && year <= lunar.GetYear(lunar.MaxSupportedDateTime))
            {
                var festival = year >= 2014 ? Substitute.Sunday : Substitute.None;
                holidays.Add(new Holiday(LunarDate(lunar, year, 1, 1), "설날", festival, true));
                holidays.Add(new Holiday(LunarDate(lunar, year, 4, 8), "부처님오신날", year >= 2023 ? Substitute.Weekend : Substitute.None));
                holidays.Add(new Holiday(LunarDate(lunar, year, 8, 15), "추석", festival, true));
            }
            var overlaps = new HashSet<DateTime>();
            foreach (var holiday in holidays)
            {
                for (var day = holiday.First; day <= holiday.Last; day = day.AddDays(1))
                {
                    if (dates.ContainsKey(day))
                    {
                        overlaps.Add(day);
                        dates[day] += " · " + holiday.Name;
                    }
                    else dates.Add(day, holiday.Name);
                }
            }
            var replacedOverlaps = new HashSet<DateTime>();
            foreach (var holiday in holidays.OrderBy(item => item.Last))
            {
                if (holiday.Rule == Substitute.None) continue;
                bool needed = false;
                DateTime overlap = DateTime.MinValue;
                for (var day = holiday.First; day <= holiday.Last; day = day.AddDays(1))
                {
                    if (day.DayOfWeek == DayOfWeek.Sunday ||
                        day.DayOfWeek == DayOfWeek.Saturday && holiday.Rule == Substitute.Weekend)
                    {
                        needed = true;
                        break;
                    }
                    if (overlaps.Contains(day) && day.DayOfWeek != DayOfWeek.Saturday)
                    {
                        needed = true;
                        overlap = day;
                        break;
                    }
                }
                if (!needed) continue;
                // Two holidays on the same weekday share one replacement date.
                // Separate weekend holidays can still produce consecutive replacements.
                if (overlap != DateTime.MinValue && !replacedOverlaps.Add(overlap)) continue;
                var next = holiday.Last.AddDays(1);
                while (dates.ContainsKey(next) || next.DayOfWeek == DayOfWeek.Saturday || next.DayOfWeek == DayOfWeek.Sunday)
                    next = next.AddDays(1);
                dates[next] = "대체공휴일 · " + holiday.Name;
            }
            return dates;
        }

        static DateTime LunarDate(KoreanLunisolarCalendar calendar, int year, int month, int day)
        {
            int leap = calendar.GetLeapMonth(year);
            if (leap != 0 && leap <= month) month++;
            return calendar.ToDateTime(year, month, day, 0, 0, 0, 0);
        }
    }
}

