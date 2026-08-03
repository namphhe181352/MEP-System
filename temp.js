const fs = require('fs');
const file = 'd:/Đồ Án/Metal-ERP/src/modules/admin/pages/AdminDashboard.jsx';
let lines = fs.readFileSync(file, 'utf8').split('\n');

// 1. Remove JSX from line 329 to 622
lines.splice(328, 294);
lines.splice(328, 0, 
  '          {/* ========================================================================= */}', 
  '          {/* 3. OWNER REPORTS TABS */}', 
  '          {/* ========================================================================= */}', 
  '          <div className=\"mt-8 border-t border-slate-200 pt-8 dark:border-[#333333]\">', 
  '            <OwnerReports />', 
  '          </div>'
);

// 2. Remove derived data lines 161 to 180 (indices 160 to 179)
lines.splice(160, 20);

// 3. Remove overview API call line 131, and its usages
const getOverviewIndex = lines.findIndex(l => l.includes('getOverview(),'));
if (getOverviewIndex !== -1) {
    lines.splice(getOverviewIndex, 1);
}
const thenIndex = lines.findIndex(l => l.includes('.then(([statsData, chartData, eventsData, overviewData]) => {'));
if (thenIndex !== -1) {
    lines[thenIndex] = "      .then(([statsData, chartData, eventsData]) => {";
}
const setOverviewIndex = lines.findIndex(l => l.includes('setOverview(overviewData);'));
if (setOverviewIndex !== -1) {
    lines.splice(setOverviewIndex, 1);
}

// 4. Remove ChartTooltip lines 102 to 115
const tooltipIndex = lines.findIndex(l => l.includes('const ChartTooltip = ({ active, payload, label }) => {'));
if (tooltipIndex !== -1) {
    lines.splice(tooltipIndex - 1, 15);
}

// 5. Replace imports
const importRechartsStart = lines.findIndex(l => l.includes('import {') && lines[l+1] && lines[l+1].includes('AreaChart'));
if (importRechartsStart !== -1) {
    lines.splice(importRechartsStart, 10);
}
const importFmtIndex = lines.findIndex(l => l.includes('import { fmtVND, fmtInt, fmtPct, fmtPeriod }'));
if (importFmtIndex !== -1) {
    lines.splice(importFmtIndex, 1, "import OwnerReports from '../../report/pages/OwnerReports';");
}

fs.writeFileSync(file, lines.join('\n'));
console.log('Update complete.');
