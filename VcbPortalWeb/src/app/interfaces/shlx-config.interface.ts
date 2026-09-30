/**
 * Một dòng Excel. SÁU trường đầu đi sang ACQHUB; NĂM trường sau ở lại portal để
 * tạo user — backend tách hai hình dạng ra trước khi ký bản tin, nên đừng gộp
 * chúng ở đây rồi tưởng backend cũng gộp.
 */
export interface ShlxConfigItem {
    merchantId: string;
    terminalId: string;
    terminalName: string;
    ddAccountNumber: string;
    branchCode: string;
    province: string;

    /** USER_NAME — bắt buộc. Không có thì không tạo được user portal. */
    userName: string;

    /** ROLE_ID — bỏ trống thì backend mặc định 28 (SHLX). */
    roleId: number | null;

    /** FULLNAME — bỏ trống thì backend lấy chính userName. */
    fullName: string;

    email: string;
    mobile: string;
}

export interface ShlxConfigPayload {
    items: ShlxConfigItem[];
}

export interface ShlxConfigResult {
    terminal_id: string;
    merchant_account_id: number;
    success: boolean;
    result_code: string;
    result_message: string;
}

/**
 * Kết quả tạo user portal của MỘT terminal. Chỉ có dòng cho terminal ACQHUB đã
 * cài xong — terminal hỏng thì không có dòng nào ở đây.
 */
export interface ShlxUserResult {
    terminal_id: string;
    user_name: string;
    created: boolean;
    message: string;
}

export interface ShlxConfigResponse {
    code: string;
    message: string;
    requestId: string;
    subCode: string;
    subMessage: string;
    serverTime: string;
    nodeOut: string;
    operation: string;
    results: ShlxConfigResult[];

    /** Portal tự điền, không đến từ ACQHUB. */
    users: ShlxUserResult[];
}
