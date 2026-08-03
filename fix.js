const fs = require('fs');
const file = 'd:/Đồ Án/Metal-ERP/src/modules/admin/pages/AdminDashboard.jsx';
let lines = fs.readFileSync(file, 'utf8').split('\n');

const divIndex = lines.findIndex(l => l.includes('<div className="mx-auto max-w-[1600px] space-y-6 pb-10 text-slate-900 dark:text-[#e5e5e5]">'));
if (divIndex !== -1) {
    if (!lines[divIndex - 1].includes('return (')) {
        lines.splice(divIndex, 0, '  return (');
        fs.writeFileSync(file, lines.join('\n'));
        console.log('Fixed syntax error');
    } else {
        console.log('Already fixed');
    }
} else {
    console.log('Not found');
}
