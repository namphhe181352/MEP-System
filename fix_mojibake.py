import os
import glob

def fix_mojibake(text):
    # This is a common encoding error where utf-8 bytes were decoded as cp1252 (or similar)
    try:
        # We try to reverse the mojibake. 
        # The text is currently a string where utf-8 bytes were misinterpreted.
        # So we encode it back to latin-1 (to get the raw bytes) and then decode as utf-8.
        return text.encode('windows-1252').decode('utf-8')
    except Exception as e:
        # Fallback to direct replacement for known strings
        replacements = {
            'Nháº­t kÃ½ há»‡ thá»‘ng': 'Nh?t ký h? th?ng',
            'ThÃ´ng bÃ¡o há»‡ thá»‘ng': 'Thông báo h? th?ng',
            'Nháº­t kÃ½ mÃ¡y chá»§': 'Nh?t ký máy ch?',
            'CÃ¢u hÃ¬nh vai trÃ²': 'C?u hình vai trò',
            'PhÃ¢n quyá» n': 'Phân quy?n',
            'ThÃªm ngÆ°á» i dÃ¹ng': 'Thêm ngu?i dùng',
            'Vui lÃ²ng nháº­p': 'Vui lòng nh?p',
            'Há»  vÃ  tÃªn': 'H? và tên',
            'Há»  vÃ\xa0 tÃªn': 'H? và tên',
            'Há»§y bá»': 'H?y b?',
            'LÆ°u NgÆ°á» i DÃ¹ng': 'Luu Ngu?i Dùng',
            'ThÃªm NgÆ°á» i DÃ¹ng Má»›i': 'Thêm Ngu?i Dùng M?i',
            'Máº­t kháº©u': 'M?t kh?u',
            'Sá»‘ Ä‘iá»‡n thoáº¡i': 'S? di?n tho?i',
            'Chá» n Loáº¡i TÃ i Khoáº£n': 'Ch?n Lo?i Tài Kho?n',
            'Chá» n Loáº¡i TÃ\xa0i Khoáº£n': 'Ch?n Lo?i Tài Kho?n',
            'Tá»‘i thiá»ƒu': 'T?i thi?u',
            'kÃ½ tá»±': 'ký t?',
            'khÃ´ng há»£p lá»‡': 'không h?p l?',
            'vÃ ': 'và',
            'tÃªn': 'tên',
            'NhÃ¡p': 'Nháp',
            'Gá»i': 'G?i',
            'HÃ nh Ä‘á»™ng': 'Hành d?ng',
            'MÃ£ má»©c Ä‘á»™': 'Mã m?c d?',
            'Nguá»“n': 'Ngu?n',
            'Thá»i gian': 'Th?i gian',
            'HÃ\xa0nh Ä‘á»™ng': 'Hành d?ng',
            'MÃ£ má»©c Ä‘á»™': 'Mã m?c d?',
            'Thá»i gian': 'Th?i gian',
            'TÃ¬m kiáº¿m': 'Tìm ki?m',
            'XUáº¤T CSV': 'XU?T CSV',
            'XUáº¤T EXCEL': 'XU?T EXCEL',
            'XÃ³a': 'Xóa',
            'Chi tiáº¿t': 'Chi ti?t',
            'Ä‘ang táº£i': 'dang t?i',
            'KhÃ´ng cÃ³': 'Không có',
            'CÃ¡c': 'Các',
            'cá»§a': 'c?a',
            'vÃ\xa0': 'và',
            'TÃ\xa0i': 'Tài',
            'Khoáº£n': 'Kho?n',
            'Email Ä‘Äƒng nháº­p': 'Email dang nh?p'
        }
        for k, v in replacements.items():
            text = text.replace(k, v)
        return text

def fix_file(filepath):
    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            content = f.read()
        
        fixed_content = fix_mojibake(content)
        
        # Hardcode some remaining replacements just in case encode/decode fails or misses edge cases
        hardcoded = {
            'Nháº­t kÃ½': 'Nh?t ký',
            'mÃ¡y chá»§': 'máy ch?',
            'há»‡ thá»‘ng': 'h? th?ng',
            'ThÃ´ng bÃ¡o': 'Thông báo',
            'ThÃªm NgÆ°á» i DÃ¹ng': 'Thêm Ngu?i Dùng',
            'Má»›i': 'M?i',
            'Há»§y bá» ': 'H?y b?',
            'LÆ°u': 'Luu',
            'NgÆ°á» i DÃ¹ng': 'Ngu?i Dùng',
            'Há»  vÃ  TÃªn': 'H? và Tên',
            'Email Ä‘Äƒng nháº­p': 'Email dang nh?p',
            'Máº­t kháº©u': 'M?t kh?u',
            'Tá»‘i thiá»ƒu 6 kÃ½ tá»±': 'T?i thi?u 6 ký t?',
            'Sá»‘ Ä‘iá»‡n thoáº¡i': 'S? di?n tho?i',
            'Chá» n Loáº¡i TÃ\xa0i Khoáº£n': 'Ch?n Lo?i Tài Kho?n',
            'Vui lÃ²ng nháº­p': 'Vui lòng nh?p',
            'khÃ´ng há»£p lá»‡': 'không h?p l?',
            'Ã\xadt nháº¥t': 'ít nh?t',
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
            'Ä\x90Ã£ gá»i': 'Ðã g?i',
            'NhÃ¡p': 'Nháp',
            'HÃ\xbat bá» ': 'H?y b?',
            'Quáº£n lÃ½': 'Qu?n lý',
            'TÃ\xacm kiáº¿m': 'Tìm ki?m',
            'Ä\x90ang táº£i': 'Ðang t?i'
        }
        for k, v in hardcoded.items():
            fixed_content = fixed_content.replace(k, v)

        if content != fixed_content:
            with open(filepath, 'w', encoding='utf-8') as f:
                f.write(fixed_content)
            print(f'Fixed: {filepath}')
    except Exception as e:
        print(f'Error reading {filepath}: {e}')

files = glob.glob('d:/Ð? Án/Metal-ERP/src/modules/admin/**/*.jsx', recursive=True)
for f in files:
    fix_file(f)

print('Done!')
