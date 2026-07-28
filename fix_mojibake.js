const fs = require('fs');
const path = require('path');

const replacements = {
    'Nháº­t KÃ½': 'Nh?t Ký',
    'Nháº­t kÃ½': 'Nh?t ký',
    'Há»‡ Thá»‘ng': 'H? Th?ng',
    'há»‡ thá»‘ng': 'h? th?ng',
    'ThÃ´ng bÃ¡o': 'Thông báo',
    'ThÃ´ng BÃ¡o': 'Thông Báo',
    'ThÃªm ngÆ°á» i dÃ¹ng': 'Thêm ngu?i dùng',
    'ThÃªm NgÆ°á» i DÃ¹ng': 'Thêm Ngu?i Dùng',
    'Má»›i': 'M?i',
    'Vui lÃ²ng nháº­p': 'Vui lòng nh?p',
    'Há»  vÃ  TÃªn': 'H? và Tên',
    'Há»  vÃ\xa0 TÃªn': 'H? và Tên',
    'Há»  vÃ  tÃªn': 'H? và tên',
    'Há»§y bá»': 'H?y b?',
    'LÆ°u': 'Luu',
    'NgÆ°á» i DÃ¹ng': 'Ngu?i Dùng',
    'Máº­t kháº©u': 'M?t kh?u',
    'Tá»‘i thiá»ƒu 6 kÃ½ tá»±': 'T?i thi?u 6 ký t?',
    'Sá»‘ Ä‘iá»‡n thoáº¡i': 'S? di?n tho?i',
    'Chá» n Loáº¡i TÃ\xa0i Khoáº£n': 'Ch?n Lo?i Tài Kho?n',
    'khÃ´ng há»£p lá»‡': 'không h?p l?',
    'Ã\xadt nháº¥t': 'ít nh?t',
    'CÃ\x81U HÃ\x8cNH VAI TRÃ\x92': 'C?U HÌNH VAI TRÒ',
    'CÃ\xa2u hÃ\xacnh': 'C?u hình',
    'PHÃ\x82N QUYÃ\x80N': 'PHÂN QUY?N',
    'PhÃ¢n quyá» n': 'Phân quy?n',
    'CHá»¨C NÄ\x82NG': 'CH?C NANG',
    'QUáº¢N LÃ\x9d': 'QU?N LÝ',
    'Quáº£n lÃ½': 'Qu?n lý',
    'DÃ\x81NH SÃ\x81CH': 'DANH SÁCH',
    'Tráº¡ng thÃ¡i': 'Tr?ng thái',
    'HÃ\xa0nh Ä‘á»™ng': 'Hành d?ng',
    'Nguá»“n': 'Ngu?n',
    'MÃ£ má»©c Ä‘á»™': 'Mã m?c d?',
    'Thá»\x9di gian': 'Th?i gian',
    'TÃ¬m kiáº¿m': 'Tìm ki?m',
    'Ä\x90Ã£ gá»i': 'Ðã g?i',
    'NhÃ¡p': 'Nháp',
    'HÃ\xbat bá» ': 'H?y b?',
    'Ä\x90ang táº£i': 'Ðang t?i',
    'KhÃ´ng cÃ³': 'Không có',
    'CÃ¡c': 'Các',
    'cá»§a': 'c?a',
    'vÃ\xa0': 'và',
    'TÃ\xa0i': 'Tài',
    'Khoáº£n': 'Kho?n',
    'Email Ä‘Äƒng nháº­p': 'Email dang nh?p',
    'KhÃ´ng táº£i Ä‘Æ°á»£c': 'Không t?i du?c',
    'danh sÃ¡ch': 'danh sách',
    'Sá»\xada': 'S?a',
    'Táº¡o': 'T?o',
    'TiÃªu Ä‘á» ': 'Tiêu d?',
    'Ná»™i dung': 'N?i dung',
    'Lá»‹ch báº£o trÃ¬': 'L?ch b?o trì',
    'chi tiáº¿t': 'chi ti?t',
    'Ä‘á»‘i tÆ°á»£ng': 'd?i tu?ng',
    'Táº¥t cáº£': 'T?t c?',
    'ngÆ°á» i dÃ¹ng': 'ngu?i dùng',
    'Chá»\x89 Chá»§': 'Ch? Ch?',
    'cá»\xada hÃ\xa0ng': 'c?a hàng',
    'Chá»\x89 NhÃ¢n viÃªn': 'Ch? Nhân viên',
    'Má»©c Ä‘á»™ Æ°u tiÃªn': 'M?c d? uu tiên',
    'Tháº¥p': 'Th?p',
    'BÃ¬nh thÆ°á» ng': 'Bình thu?ng',
    'Cao (Kháº©n cáº¥p)': 'Cao (Kh?n c?p)',
    'LÃªn lá»‹ch gá»\xadi': 'Lên l?ch g?i',
    'KhÃ´ng báº¯t buá»™c': 'Không b?t bu?c',
    'Há»\xa7y': 'H?y',
    'Cáº\xadp nháº\xadt': 'C?p nh?t',
    'thÃ\xa0nh cÃ´ng': 'thành công',
    'Thao tÃ¡c': 'Thao tác',
    'tháº¥t báº¡i': 'th?t b?i',
    'Báº¡n cÃ³ cháº¯c cháº¯n': 'B?n có ch?c ch?n',
    'muá»‘n': 'mu?n',
    'PHÃ\x81T SÃ\x93NG': 'PHÁT SÓNG',
    'ngay láº\xadp tá»\xadc': 'ngay l?p t?c',
    'Ä\x90Ã£ gá»\xadi': 'Ðã g?i',
    'Lá»\x97i': 'L?i',
    'XÃ³a vÄ\xa9nh viá»\x85n': 'Xóa vinh vi?n',
    'Ä\x90Ã£ xÃ³a': 'Ðã xóa',
    'QUáº¢N LÃ\x9d VÃ\x80 PHÃ\x81T SÃ\x93NG': 'QU?N LÝ VÀ PHÁT SÓNG',
    'CHO NGÆ¯á»\x9cI DÃ\x99NG': 'CHO NGU?I DÙNG',
    'Æ¯u tiÃªn': 'Uu tiên',
    'nÃ\xa0o.': 'nào.',
    'nÃ\xa0y': 'này',
    'Gá»\xadi ngay': 'G?i ngay'
};

function fixMojibake(text) {
    try {
        const buffer = Buffer.from(text, 'utf-8');
        const latin1 = buffer.toString('latin1');
        const fixed = Buffer.from(latin1, 'latin1').toString('utf-8');
        return fixed;
    } catch (e) {}
    return text;
}

function processFile(filePath) {
    try {
        const content = fs.readFileSync(filePath, 'utf-8');
        
        // 1. Try automatic reversal
        let newContent = content;
        // The automatic reversal might be risky if file is already partially UTF-8. 
        // We will just use string replacement for the known tokens.
        for (const [bad, good] of Object.entries(replacements)) {
            newContent = newContent.replaceAll(bad, good);
        }

        // Apply some additional common ones found in logs
        newContent = newContent.replaceAll('Ä‘', 'd')
                               .replaceAll('áº­', '?')
                               .replaceAll('Ã¡', 'á')
                               .replaceAll('Ã', 'í')
                               .replaceAll('Ã´', 'ô')
                               .replaceAll('á»‡', '?')
                               .replaceAll('á»‘', '?')
                               .replaceAll('á»±', '?')
                               .replaceAll('á»', '?')
                               .replaceAll('Ã£', 'ã')
                               .replaceAll('Æ°', 'u')
                               .replaceAll('á»£', '?')
                               .replaceAll('á» ', '?')
                               .replaceAll('Ãª', 'ê');

        if (content !== newContent) {
            // Re-read and replace manually to avoid destroying non-mojibake characters
            let safeContent = fs.readFileSync(filePath, 'utf-8');
            let changed = false;

            const strictReplacements = {
                'Nháº­t KÃ½ Há»‡ Thá»‘ng': 'Nh?t Ký H? Th?ng',
                'ThÃ´ng BÃ¡o Há»‡ Thá»‘ng': 'Thông Báo H? Th?ng',
                'ThÃ´ng bÃ¡o': 'Thông báo',
                'ThÃªm NgÆ°á» i DÃ¹ng': 'Thêm Ngu?i Dùng',
                'Vui lÃ²ng nháº­p': 'Vui lòng nh?p',
                'Há»§y bá» ': 'H?y b?',
                'LÆ°u': 'Luu',
                'Máº­t kháº©u': 'M?t kh?u',
                'Sá»‘ Ä‘iá»‡n thoáº¡i': 'S? di?n tho?i',
                'Chá» n Loáº¡i TÃ\xa0i Khoáº£n': 'Ch?n Lo?i Tài Kho?n',
                'Chá» n Loáº¡i TÃ\x20i Khoáº£n': 'Ch?n Lo?i Tài Kho?n',
                'khÃ´ng há»£p lá»‡': 'không h?p l?',
                'CÃ\x81U HÃ\x8cNH VAI TRÃ\x92': 'C?U HÌNH VAI TRÒ',
                'PHÃ\x82N QUYÃ\x80N': 'PHÂN QUY?N',
                'CHá»¨C NÄ\x82NG': 'CH?C NANG',
                'QUáº¢N LÃ\x9d': 'QU?N LÝ',
                'DÃ\x81NH SÃ\x81CH': 'DANH SÁCH',
                'Tráº¡ng thÃ¡i': 'Tr?ng thái',
                'HÃ\xa0nh Ä‘á»™ng': 'Hành d?ng',
                'Nguá»“n': 'Ngu?n',
                'MÃ£ má»©c Ä‘á»™': 'Mã m?c d?',
                'Thá»\x9di gian': 'Th?i gian',
                'TÃ¬m kiáº¿m': 'Tìm ki?m',
                'Ä\x90Ã£ gá»\xadi': 'Ðã g?i',
                'NhÃ¡p': 'Nháp',
                'Ä\x90ang táº£i': 'Ðang t?i',
                'KhÃ´ng cÃ³': 'Không có',
                'KhÃ´ng táº£i Ä‘Æ°á»£c': 'Không t?i du?c',
                'danh sÃ¡ch': 'danh sách',
                'TiÃªu Ä‘á» ': 'Tiêu d?',
                'Ná»™i dung': 'N?i dung',
                'Lá»‹ch báº£o trÃ¬': 'L?ch b?o trì',
                'chi tiáº¿t': 'chi ti?t',
                'Ä‘á»‘i tÆ°á»£ng': 'd?i tu?ng',
                'Táº¥t cáº£': 'T?t c?',
                'ngÆ°á» i dÃ¹ng': 'ngu?i dùng',
                'Chá»\x89 Chá»§': 'Ch? Ch?',
                'cá»\xada hÃ\xa0ng': 'c?a hàng',
                'Chá»\x89 NhÃ¢n viÃªn': 'Ch? Nhân viên',
                'Má»©c Ä‘á»™ Æ°u tiÃªn': 'M?c d? uu tiên',
                'Tháº¥p': 'Th?p',
                'BÃ¬nh thÆ°á» ng': 'Bình thu?ng',
                'Cao (Kháº©n cáº¥p)': 'Cao (Kh?n c?p)',
                'LÃªn lá»‹ch gá»\xadi': 'Lên l?ch g?i',
                'KhÃ´ng báº¯t buá»™c': 'Không b?t bu?c',
                'Há»\xa7y': 'H?y',
                'Cáº\xadp nháº\xadt': 'C?p nh?t',
                'thÃ\xa0nh cÃ´ng': 'thành công',
                'Thao tÃ¡c': 'Thao tác',
                'tháº¥t báº¡i': 'th?t b?i',
                'Báº¡n cÃ³ cháº¯c cháº¯n': 'B?n có ch?c ch?n',
                'muá»‘n': 'mu?n',
                'PHÃ\x81T SÃ\x93NG': 'PHÁT SÓNG',
                'ngay láº\xadp tá»\xadc': 'ngay l?p t?c',
                'Lá»\x97i': 'L?i',
                'XÃ³a vÄ\xa9nh viá»\x85n': 'Xóa vinh vi?n',
                'Ä\x90Ã£ xÃ³a': 'Ðã xóa',
                'QUáº¢N LÃ\x9d VÃ\x80 PHÃ\x81T SÃ\x93NG': 'QU?N LÝ VÀ PHÁT SÓNG',
                'CHO NGÆ¯á»\x9cI DÃ\x99NG': 'CHO NGU?I DÙNG',
                'Æ¯u tiÃªn': 'Uu tiên',
                'nÃ\xa0o.': 'nào.',
                'nÃ\xa0y': 'này',
                'Gá»\xadi ngay': 'G?i ngay',
                'MÃ£': 'Mã',
                'HÃ\xa0nh': 'Hành',
                'Nguá»“n': 'Ngu?n'
            };

            for (const [bad, good] of Object.entries(strictReplacements)) {
                if (safeContent.includes(bad)) {
                    safeContent = safeContent.replaceAll(bad, good);
                    changed = true;
                }
            }
            
            // Also attempt global buffer decoding for the rest of the file
            // A common trick is to encode in latin1 and decode in utf-8
            try {
                // Find sequences of mojibake by looking for high bytes
                const re = /[\x80-\xff]{2,}/g;
                safeContent = safeContent.replace(re, match => {
                    try {
                        const b = Buffer.from(match, 'latin1');
                        const decoded = b.toString('utf8');
                        if (decoded.includes('')) return match; // Invalid utf-8, ignore
                        return decoded;
                    } catch { return match; }
                });
                changed = true;
            } catch (e) {}

            if (changed) {
                fs.writeFileSync(filePath, safeContent, 'utf-8');
                console.log('Fixed:', filePath);
            }
        }
    } catch (e) {
        console.error('Error processing file:', filePath, e);
    }
}

function walk(dir) {
    const list = fs.readdirSync(dir);
    list.forEach(file => {
        const filePath = path.join(dir, file);
        const stat = fs.statSync(filePath);
        if (stat && stat.isDirectory()) {
            walk(filePath);
        } else if (filePath.endsWith('.jsx')) {
            processFile(filePath);
        }
    });
}

walk('d:/Ð? Án/Metal-ERP/src/modules/admin');
console.log('Finished');
