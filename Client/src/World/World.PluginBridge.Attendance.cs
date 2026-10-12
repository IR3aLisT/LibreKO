using System;
using System.Collections.Generic;
using LibreKO.Network;
using LibreKO.Plugins;

namespace LibreKO;

public partial class World
{
    private void PluginNotifyAttendance() => Godot.Callable.From(PluginHost.Game.RaiseAttendance).CallDeferred();

    private GameAttendanceCell AttendanceCellAt(int index)
    {
        int slot = _attendanceSlots[index];
        byte state = _attendanceStates[index];
        var (itemId, count) = ItemData.AttendanceReward(slot);
        var look = state is Net.AttendanceStateClaimed or Net.AttendanceStateExpired ? GameAttendanceState.Claimed
            : state is >= Net.AttendanceStateClaimable and <= Net.AttendanceStateClaimableExtra ? GameAttendanceState.Claimable
            : GameAttendanceState.Locked;
        return new GameAttendanceCell(itemId, count, itemId > 0 ? ItemData.DisplayName(itemId) : "",
            itemId > 0 ? ItemData.Icon(itemId) : null, AttendanceSlotCaption(slot, state), look);
    }

    private int AttendedDays()
    {
        int attended = 0;
        for (int i = 0; i < Net.AttendanceDailySlots; i++)
            if (_attendanceStates[i] is Net.AttendanceStateClaimed or Net.AttendanceStateExpired) attended++;
        return attended;
    }

    private void ClaimAttendanceCell(int index)
    {
        if (index < 0 || index >= _attendanceSlots.Length) return;
        byte state = _attendanceStates[index];
        if (state is < Net.AttendanceStateClaimable or > Net.AttendanceStateClaimableExtra) return;
        Net.I.SendAttendanceClaim(_attendanceSlots[index], state);
    }

    private void ShowAttendanceTooltip(int index)
    {
        if (index < 0 || index >= _attendanceSlots.Length) return;
        var (itemId, count) = ItemData.AttendanceReward(_attendanceSlots[index]);
        if (itemId <= 0) return;
        ShowItemTooltip(-1, new ItemSlot
        {
            ItemId = itemId,
            Count = (short)count,
            Durability = (short)(ItemData.Get(itemId)?.Duration ?? 0),
        });
    }

    private sealed class PluginAttendanceBridge : IGameAttendance
    {
        private readonly World _w;

        public PluginAttendanceBridge(World w) => _w = w;

        public bool IsOpen => _w._attendanceShown;
        public int Attended => _w.AttendedDays();

        public IReadOnlyList<GameAttendanceCell> Daily
        {
            get
            {
                var cells = new GameAttendanceCell[Net.AttendanceDailySlots];
                for (int i = 0; i < cells.Length; i++) cells[i] = _w.AttendanceCellAt(i);
                return cells;
            }
        }

        public IReadOnlyList<GameAttendanceCell> Bonus
        {
            get
            {
                var cells = new GameAttendanceCell[Net.AttendanceBonusSlots];
                for (int i = 0; i < cells.Length; i++) cells[i] = _w.AttendanceCellAt(Net.AttendanceDailySlots + i);
                return cells;
            }
        }

        public void Claim(int index) => _w.ClaimAttendanceCell(index);
        public void ShowTooltip(int index) => _w.ShowAttendanceTooltip(index);
        public void HideTooltip() => _w.HideItemTooltip();
        public void Close() => _w.CloseAttendance();

        event Action? IGameAttendance.Changed { add { } remove { } }
    }
}
