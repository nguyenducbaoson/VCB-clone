import { inject, Injectable } from '@angular/core';
import {
    ShlxConfigItem,
    ShlxConfigPayload,
    ShlxConfigResponse,
    ShlxConfigResult,
    ShlxUserResult,
} from 'app/interfaces/shlx-config.interface';
import { RequestService } from 'app/services/request.service';
import { environment } from 'environments/environment';

/**
 * Kết quả một lô. Màn hình gọi tự quyết hiển thị thế nào — service không đụng
 * tới giao diện, không bật toast, không đóng dialog.
 */
export interface ShlxKetQuaLo {
    /** Số dòng đã gửi đi. */
    daGui: number;

    /** Số terminal ACQHUB cài xong. */
    terminalXong: number;

    /** Số user portal vừa tạo. */
    userDaTao: number;

    /**
     * terminalId -> lý do ACQHUB từ chối. Sửa rồi gửi lại được.
     */
    loiTerminal: Map<string, string>;

    /**
     * terminalId -> vì sao không tạo được user, DÙ terminal đã cài xong.
     * KHÁC HẲN loiTerminal: gửi lại vô ích, ACQHUB sẽ báo "already exist".
     * Phải tạo user bằng màn hình quản lý người dùng.
     */
    loiUser: Map<string, string>;

    /** Mật khẩu ban đầu backend cấp cho user mới, để báo lại cho họ. */
    matKhauBanDau: string;
}

/**
 * Kiểm một dòng. Trả về danh sách vấn đề, rỗng là hợp lệ.
 *
 * USER_NAME LÀ CÔNG TẮC: có thì dòng đó tạo user, không có thì chỉ cài terminal.
 * 5 cột user không bắt buộc.
 */
export function kiemTraDongShlx(x: ShlxConfigItem): string[] {
    const thieu = ([
        ['TERMINAL_ID', x.terminalId],
        ['DD_ACCOUNT_NUMBER', x.ddAccountNumber],
        ['TERMINAL_NAME', x.terminalName],
        ['MERCHANT_ID', x.merchantId],
        ['PROVINCE', x.province],
        ['BRANCH_CODE', x.branchCode],
    ] as const)
        .filter(([, v]) => !v)
        .map(([ten]) => ten);

    const vanDe: string[] = [];

    if (thieu.length) vanDe.push('Thiếu: ' + thieu.join(', '));

    // roleId KHÔNG tính vào đây — nó là trường duy nhất có giá trị mặc định,
    // nên nó nói lên ý định của người viết code chứ không phải của người dùng.
    const coDuLieuUser = !!(x.userName || x.fullName || x.email || x.mobile);

    if (!x.userName) {
        // Gõ EMAIL/MOBILE mà quên USER_NAME thì backend bỏ qua LẶNG LẼ: terminal
        // vẫn cài, user không có, không ai báo gì.
        if (coDuLieuUser) vanDe.push('Có dữ liệu user nhưng thiếu USER_NAME');
        return vanDe;
    }

    // Backend chỉ nhận role SHLX. Bắt ở đây thay vì để backend từ chối, vì lúc
    // backend từ chối thì ACQHUB đã cài terminal xong rồi — dòng đó thành "đã
    // cài mà không có user", không sửa được bằng cách gửi lại.
    if (x.roleId !== null) {
        if (!Number.isFinite(x.roleId)) {
            vanDe.push('ROLE_ID không phải số');
        } else if (x.roleId !== 28 && x.roleId !== 29) {
            vanDe.push(`ROLE_ID ${x.roleId} không phải role SHLX (28, 29)`);
        }
    }

    // KeepSafe() bên backend chỉ lọc ký tự lạ, không kiểm cấu trúc. Email sai
    // vẫn lưu, và chỉ lộ ra hàng tháng sau khi user quên mật khẩu mà thư không tới.
    if (x.email && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(x.email)) {
        vanDe.push('EMAIL sai định dạng');
    }

    // Backend làm KeepNumber().PadLeft(10,'0').Trunc(10):
    //   9 chữ số   -> Excel nuốt số 0 đầu, PadLeft trả lại đúng. Cho qua.
    //   >10 chữ số -> Trunc CẮT MẤT CHỮ SỐ CUỐI, lưu nhầm số mà không báo gì.
    const soDt = (x.mobile ?? '').replace(/\D/g, '');

    if (x.mobile) {
        if (soDt.length > 10) {
            vanDe.push(`MOBILE ${soDt.length} chữ số, backend sẽ cắt còn 10`);
        } else if (soDt.length < 9) {
            vanDe.push(`MOBILE chỉ có ${soDt.length} chữ số`);
        }
    }

    return vanDe;
}

/**
 * Kiểm cả lô. Trả về CHỈ SỐ DÒNG -> lý do, để màn hình tô đỏ đúng dòng.
 *
 * Dòng trắng hoàn toàn bị bỏ qua, không tính là lỗi — file Excel đọc theo
 * .range() luôn kèm hàng nghìn dòng trống phía sau.
 */
export function kiemTraLoShlx(items: ShlxConfigItem[]): Map<number, string> {
    const loi = new Map<number, string>();

    // USER_NAME trùng trong cùng một file: MP_SHLX_USERS khoá theo USER_NAME và
    // giữ MỘT terminalId, nên chỉ dòng đầu tạo được user, dòng sau nhận "đã tồn
    // tại" rồi im lặng — terminal thứ hai cài xong mà không ai đăng nhập được.
    const demTen = new Map<string, number>();

    for (const x of items) {
        const u = (x.userName || '').toUpperCase();
        if (u) demTen.set(u, (demTen.get(u) ?? 0) + 1);
    }

    items.forEach((x, i) => {
        if (laDongTrang(x)) return;

        const vanDe = kiemTraDongShlx(x);

        if (x.userName && (demTen.get(x.userName.toUpperCase()) ?? 0) > 1) {
            vanDe.push('USER_NAME trùng với dòng khác trong file');
        }

        if (vanDe.length) loi.set(i, vanDe.join('. '));
    });

    return loi;
}

/**
 * Chuyển một dòng của lưới lớn (dataSource()) thành ShlxConfigItem.
 *
 * Lưới đó dựng cột động nên tên khoá mỗi dòng tuỳ file Excel. Hàm này tra vài
 * biến thể thường gặp — TERMINAL_ID, terminal_id, terminalId.
 *
 * KHÔNG đoán theo VỊ TRÍ cột: lưới lớn cho phép đổi thứ tự, mà đoán sai vị trí
 * thì dữ liệu vào nhầm ô và không lỗi nào báo.
 */
export function chuyenDongShlx(row: any): ShlxConfigItem {
    const lay = (ten: string): string => {
        const bienThe = [
            ten, // TERMINAL_ID
            ten.toLowerCase(), // terminal_id
            ten.toLowerCase().replace(/_(.)/g, (_, c) => c.toUpperCase()), // terminalId
        ];

        for (const k of bienThe) {
            const v = row?.[k];
            if (v !== null && v !== undefined && String(v).trim() !== '') {
                return String(v).trim();
            }
        }

        return '';
    };

    const role = lay('ROLE_ID');

    return {
        terminalId: lay('TERMINAL_ID'),
        ddAccountNumber: lay('DD_ACCOUNT_NUMBER'),
        terminalName: lay('TERMINAL_NAME'),
        merchantId: lay('MERCHANT_ID'),
        province: lay('PROVINCE'),

        // Excel trả BRANCH_CODE kiểu số thì mất số 0 đầu. Đệm lại 5 ký tự, đúng
        // như dialog đang làm.
        branchCode: lay('BRANCH_CODE').padStart(5, '0'),

        userName: lay('USER_NAME'),

        // Rỗng -> null, để backend dùng mặc định 28. Number('') là 0, mà 0 là một
        // ROLE_ID "hợp lệ" nên backend sẽ từ chối cả dòng.
        roleId: role ? Number(role) : null,

        fullName: lay('FULLNAME'),
        email: lay('EMAIL'),
        mobile: lay('MOBILE'),
    };
}

/**
 * File Excel đang mở có phải file CẤU HÌNH TERMINAL không?
 *
 * Màn hình cha dùng chung một nút "Chọn file" cho hai loại file khác hẳn nhau —
 * kết quả sát hạch (STT, Ngày SHLX, Họ, Tên...) và cấu hình terminal. Phải biết
 * loại nào trước khi dựng schema, vì đọc nhầm schema thì dữ liệu vào sai cột mà
 * không lỗi nào báo.
 *
 * Nhận theo DÒNG TIÊU ĐỀ chứ không theo số cột: hai file đều có thể có 11–14 cột
 * tuỳ lúc, còn chữ TERMINAL_ID thì chỉ file kia mới có.
 *
 * `docDongDau` là hàm do màn hình truyền vào, trả về dòng 1 của sheet dưới dạng
 * mảng chuỗi — để hàm này không phải biết gì về `worksheet`.
 */
export function laFileCauHinhTerminal(docDongDau: () => string[]): boolean {
    let tieuDe: string[];

    try {
        tieuDe = docDongDau();
    } catch {
        // Đọc hỏng thì coi như KHÔNG phải file terminal, để màn hình đi tiếp
        // luồng cũ của nó thay vì chặn người dùng lại.
        return false;
    }

    const chuan = tieuDe.map((x) => String(x ?? '').trim().toUpperCase());

    return chuan.includes('TERMINAL_ID') && chuan.includes('MERCHANT_ID');
}

export function laDongTrang(x: ShlxConfigItem): boolean {
    return !(
        x.terminalId ||
        x.ddAccountNumber ||
        x.terminalName ||
        x.merchantId ||
        x.province ||
        x.branchCode ||
        x.userName ||
        x.fullName ||
        x.email ||
        x.mobile
    );
}

/**
 * Gửi một lô terminal SHLX sang ACQHUB, và tạo user portal cho dòng nào có
 * USER_NAME.
 *
 * Dùng cho lưới lớn ở màn hình cha. Dialog "Single request" đi đường riêng của
 * nó cho một terminal.
 */
@Injectable({ providedIn: 'root' })
export class ShlxBatchService {
    private _requestService = inject(RequestService);

    /** Mật khẩu backend cấp cho user tạo hôm nay. Công thức cố định phía backend. */
    get matKhauBanDau(): string {
        const d = new Date();
        const p = (n: number) => String(n).padStart(2, '0');
        return `Shlx@${p(d.getFullYear() % 100)}${p(d.getMonth() + 1)}${p(d.getDate())}`;
    }

    /**
     * Gửi lô. NÉM nếu mạng/HTTP hỏng — màn hình gọi tự bắt và hiện thông báo.
     *
     * KHÔNG tự kiểm dữ liệu: gọi kiemTraLoShlx() trước và chặn nếu còn dòng hỏng.
     * ACQHUB ghi cấu hình ở hệ thống ngoài, không rollback được, nên thả lên rồi
     * sửa sau thì đã muộn.
     */
    async guiLo(items: ShlxConfigItem[]): Promise<ShlxKetQuaLo> {
        const canGui = items.filter((x) => !laDongTrang(x));

        // environment.maxItemPerApi có thể không tồn tại. Thiếu nó thì
        // Math.ceil(n / undefined) ra NaN và vòng lặp không chạy lần nào — không
        // request, không lỗi, không gì cả.
        const size = environment.maxItemPerApi || 100;
        const total = Math.ceil(canGui.length / size);

        const results: ShlxConfigResult[] = [];
        const users: ShlxUserResult[] = [];

        for (let i = 0; i < total; i++) {
            const payload: ShlxConfigPayload = {
                items: canGui.slice(i * size, (i + 1) * size),
            };

            const res = (await this._requestService.post(
                environment.mainEndpoint + 'acqh/para/shlx?_=' + Date.now(),
                payload
            )) as ShlxConfigResponse;

            results.push(...(res?.results ?? []));
            users.push(...(res?.users ?? []));
        }

        // Dòng CỐ Ý không tạo user. Backend vẫn trả "Thieu USER_NAME" cho chúng —
        // đúng theo nó, nhưng với người dùng thì đó là điều họ đã chọn.
        const coYeuCauUser = new Set(
            canGui.filter((x) => x.userName).map((x) => x.terminalId)
        );

        const hong = results.filter((r) => !r.success);

        const userHong = users.filter(
            (u) => !u.created && coYeuCauUser.has(u.terminal_id)
        );

        return {
            daGui: canGui.length,
            terminalXong: results.length - hong.length,
            userDaTao: users.filter((u) => u.created).length,

            loiTerminal: new Map(
                hong.map((r) => [
                    r.terminal_id,
                    `[${r.result_code}] ${r.result_message}`,
                ])
            ),

            loiUser: new Map(
                userHong.map((u) => [
                    u.terminal_id,
                    `Đã cài terminal nhưng CHƯA có user: ${u.message}`,
                ])
            ),

            matKhauBanDau: this.matKhauBanDau,
        };
    }
}
