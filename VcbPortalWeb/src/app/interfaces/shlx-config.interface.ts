export interface ShlxConfigItem {
    merchantId: string;
    terminalId: string;
    terminalName: string;
    ddAccountNumber: string;
    branchCode: string;
    province: string;
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
}
