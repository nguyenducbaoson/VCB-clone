import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import {
    AfterViewInit,
    Component,
    effect,
    signal,
    ViewChild,
} from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { FuseConfirmationService } from '@fuse/services/confirmation';
import { DataGridHelper } from 'app/helpers/data-grid.helper';
import { GetErrorText, ToastNotify } from 'app/helpers/common.helper';
import { ShlxConfigItem } from 'app/interfaces/shlx-config.interface';
import { ShlxConfigComponent } from 'app/modules/acqhub/shlx-config/ishlx-config.component';
import { RequestService } from 'app/services/request.service';
import {
    docO,
    kiemTraLoShlx,
    laDongTrang,
    laFileCauHinhTerminal,
    ShlxBatchService,
} from 'app/services/shlx-batch.service';
import { ez, ezTable, injectWorkSheet } from 'app/shared/state/excel';
import { url } from 'app/const/api-url';
import { DxDataGridComponent, DxDataGridModule } from 'devextreme-angular';
import { environment } from 'environments/environment';
import { isArray } from 'lodash';
import { lastValueFrom } from 'rxjs';
import moment from 'moment';

interface ImportShlxPayload {
    batchNo: number;
    index: number;
    total: number;
    totalCount: number;
    dataCount: number;
    data: string[][];
}

interface ImportShlxResponse {
    success: number;
    error: number;
    desc: string;
}

/**
 * Nhập dữ liệu từ file Excel — MỘT nút "Chọn file" cho HAI loại file:
 *
 *   1. Kết quả sát hạch  (STT, Ngày SHLX, Họ, Tên, CCCD, Hạng...) -> url.bca.shlx
 *   2. Cấu hình terminal (TERMINAL_ID, MERCHANT_ID...)            -> acqh/para/shlx
 *
 * Nhận loại nào bằng DÒNG TIÊU ĐỀ, không bằng số cột: hai file đều có thể 11–14
 * cột tuỳ lúc, còn chữ TERMINAL_ID thì chỉ file thứ hai mới có. Đọc nhầm schema
 * là dữ liệu vào sai cột mà không lỗi nào báo.
 */
@Component({
    selector: 'import-shlx',
    templateUrl: './ishlx.component.html',
    standalone: true,
    imports: [
        ReactiveFormsModule,
        MatButtonModule,
        MatDialogModule,
        MatFormFieldModule,
        MatIconModule,
        MatSelectModule,
        DxDataGridModule,
    ],
})
export class ImportShlxComponent implements AfterViewInit {
    @ViewChild(DxDataGridComponent) dataGrid!: DxDataGridComponent;

    worksheet = injectWorkSheet();
    selectControl = new FormControl<number>(0);

    dataSource = signal<any[]>([]);
    columns = signal<
        { key: string; label: string; dataType: any; format: any }[]
    >([]);

    // ── Phần thêm cho luồng cấu hình terminal ──────────────────────────────

    laFileTerminal = signal<boolean>(false);

    /** Dữ liệu terminal đã chuyển kiểu, dùng để gửi. dataSource chỉ để hiển thị. */
    private _terminals = signal<ShlxConfigItem[]>([]);

    /** Chỉ số dòng -> lý do. Chặn không cho gửi. */
    loiDong = signal<Map<number, string>>(new Map());

    dangGui = signal<boolean>(false);

    constructor(
        private _requestService: RequestService,
        private _httpClient: HttpClient,
        private dialog: MatDialog,
        private fuseDialog: FuseConfirmationService,
        private _shlx: ShlxBatchService
    ) {
        this.setDefault();

        effect(() => {
            const list = this.worksheet.sheetList();
            this.selectControl.setValue(list[0]?.id);
        });
    }

    setDefault() {
        if (this.dataGrid && this.dataGrid.dataSource)
            this.dataGrid.dataSource = undefined;
    }

    ngAfterViewInit(): void {
        DataGridHelper.setupGridImport(this.dataGrid);
    }

    onFileSelected(e: any): void {
        if (!e.target || !e.target.files || e.target.files.length <= 0) return;

        this.setDefault();

        this.dataSource.set([]);
        this.columns.set([]);
        this._terminals.set([]);
        this.loiDong.set(new Map());
        this.laFileTerminal.set(false);

        const file: File = e.target.files[0];

        if (file) this.worksheet.load(file);
    }

    openShlxConfig(): void {
        this.dialog.open(ShlxConfigComponent, {
            panelClass: 'shlx-config-dialog',
            disableClose: true,
            autoFocus: false,
            maxWidth: '42rem',
            width: '94vw',
        });
    }

    readFile(fileResource: Blob) {
        return new Promise((resolve) => {
            const reader = new FileReader();
            reader.readAsArrayBuffer(fileResource);
            reader.onload = () => {
                resolve(reader.result);
            };
        });
    }

    selectSheet() {
        if (this.selectControl.value >= 0) {
            this.dataGrid.dataSource = undefined;
        }

        const sheetSelect = this.worksheet
            .sheetList()
            .find((s) => s.id == this.selectControl.value);

        if (!sheetSelect) return;

        // Rẽ nhánh TRƯỚC khi dựng schema. Dựng nhầm schema cho file kia thì
        // transformToNumber gặp "V827308100" và cả file hỏng mà thông báo lại
        // nói về STT — không ai lần ra được.
        if (laFileCauHinhTerminal(() => this._docDongTieuDe(sheetSelect.id))) {
            this.laFileTerminal.set(true);
            this._docFileTerminal(sheetSelect.id);
            return;
        }

        this.laFileTerminal.set(false);
        this._docFileKetQua(sheetSelect.id);
    }

    // ── Luồng cũ: kết quả sát hạch ─────────────────────────────────────────

    private _docFileKetQua(sheetId: number) {
        const transformToNumber = (input: unknown) => {
            if (typeof input === 'number') return input;
            if (String(input ?? '') === '') return null;
            return +String(input ?? '').replace(',', '');
        };

        const transformDate = (val: any) => {
            const date = val
                ? val instanceof Date
                    ? val
                    : moment(
                          String(val)
                              .split('/')
                              .map((item: string) => item.padStart(2, '0'))
                              .join('/'),
                          'DD/MM/YYYY'
                      ).toDate()
                : null;

            if (date instanceof Date && isNaN(date.getTime()))
                throw new Error('Ngày định dạng không hợp lệ');

            return date;
        };

        const tableConfig = ezTable([
            ez
                .preprocess(
                    transformToNumber,
                    ez.number().onError({ type: 'skip_row' }).required()
                )
                .meta({ label: 'STT' }),
            ez.preprocess(transformDate, ez.date().required()).meta({
                label: 'Ngày SHLX',
                format: 'dd/MM/yyyy',
            }),
            ez
                .preprocess((s: unknown) => String(s), ez.string())
                .meta({ label: 'Họ' })
                .onError({ type: 'throw' }),
            ez
                .preprocess((s: unknown) => String(s), ez.string())
                .meta({ label: 'Tên' })
                .onError({ type: 'throw' }),
            ez
                .preprocess(transformDate, ez.date().required())
                .meta({ label: 'Ngày sinh', format: 'dd/MM/yyyy' }),
            ez
                .preprocess(
                    (s: unknown) => String(s),
                    ez.string().max(12, 'CCDD không hợp lệ')
                )
                .meta({ label: 'CCCD' }),
            ez
                .preprocess((s: unknown) => String(s), ez.string())
                .meta({ label: 'Hạng' }),
            ez
                .preprocess((s: unknown) => String(s), ez.string())
                .meta({ label: 'Ghi chú' }),
            ez.preprocess(transformToNumber, ez.number()).meta({
                label: 'Luật',
                format: { type: '' },
            }),
            ez.preprocess(transformToNumber, ez.number()).meta({
                label: 'Mô phỏng',
                format: '#,##0',
            }),
            ez.preprocess(transformToNumber, ez.number()).meta({
                label: 'Hình',
                format: '#,##0',
            }),
            ez.preprocess(transformToNumber, ez.number()).meta({
                label: 'Đường',
                format: '#,##0',
            }),
            ez.preprocess(transformToNumber, ez.number()).meta({
                label: 'Cấp GPLX',
                format: '#,##0',
            }),
            ez.preprocess(transformToNumber, ez.number()).meta({
                label: 'Tổng tiền',
                format: '#,##0',
            }),
        ])
            .range(2, 10_000)
            .asArray();

        const result = this.worksheet.get(sheetId, tableConfig);

        if (result.issues.length) {
            this.fuseDialog.open({
                title: `Có ${result.issues.length} lỗi`,
                message: `${result.issues
                    .map(
                        (item: any) =>
                            `[Dòng ${item.path[0]}, Cột ${item.path[1]}]: ${item.message}` +
                            `<br> - loại lỗi: ${item.code}` +
                            `<br> - giá trị: ${item.reveice}` +
                            `<br> - kiểu dữ liệu: ${typeof item.reveice}`
                    )
                    .join('<br>')}`,
                icon: { color: 'error' },
                actions: { cancel: { show: false }, confirm: { label: 'Xác nhận' } },
            });
            return;
        }

        const columns = tableConfig.columns.map(
            (item: any, index: number) =>
                ({ ...item.getMeta(), key: String(index) }) as any
        );

        this.dataSource.set(result.data);
        this.columns.set(columns);
    }

    // ── Luồng mới: cấu hình terminal ───────────────────────────────────────

    /** Dòng 1 của sheet, dạng mảng chuỗi. Chỉ để nhận loại file. */
    private _docDongTieuDe(sheetId: number): string[] {
        const cfg = ezTable(
            Array.from({ length: 14 }, () =>
                ez.preprocess(
                    (v: unknown) => String(v ?? '').trim(),
                    ez.string()
                )
            )
        )
            .range(1, 1)
            .asArray();

        return ((this.worksheet.get(sheetId, cfg).data as string[][])[0] ??
            []) as string[];
    }

    private _docFileTerminal(sheetId: number) {
        // docO, khong phai String(): o Excel co the la hyperlink, rich text hay
        // cong thuc — String() cua chung ra '[object Object]'.
        const asText = docO;

        // Đệm số 0 cho BRANCH_CODE, nhớ theo CHỈ SỐ DÒNG chứ không theo giá trị:
        // sau khi đệm thì '01400' gõ tay và '01400' vừa đệm giống hệt nhau.
        const daDem = new Set<number>();
        let dongBranch = 0;

        const asBranchCode = (v: unknown) => {
            const dong = dongBranch++;
            const s = asText(v);
            if (typeof v !== 'number' || s.length >= 5) return s;
            daDem.add(dong);
            return s.padStart(5, '0');
        };

        // KHÔNG cột nào .required(). Đã đo trên `ez` thật: một ô required trượt
        // thì CẢ DÒNG bị loại khỏi result.data, kể cả với onError 'skip_cell'.
        // Dòng thiếu TERMINAL_ID sẽ biến mất thay vì hiện đỏ, và người dùng chỉ
        // thấy "file không có dòng nào".
        const cot = (label: string) =>
            ez.preprocess(asText, ez.string()).meta({ label });

        const tableConfig = ezTable([
            cot('TERMINAL_ID'),
            cot('DD_ACCOUNT_NUMBER'),
            cot('TERMINAL_NAME'),
            cot('MERCHANT_ID'),
            cot('PROVINCE'),
            ez.preprocess(asBranchCode, ez.string()).meta({ label: 'BRANCH_CODE' }),
            cot('USER_NAME'),
            cot('ROLE_ID'),
            cot('FULLNAME'),
            cot('EMAIL'),
            cot('MOBILE'),
        ])
            .range(2, 10_000)
            .asArray();

        const result = this.worksheet.get(sheetId, tableConfig);

        // .range(2, 10_000) luôn trả 9999 dòng, phần lớn trống. Bỏ trước khi làm
        // gì khác, không thì mỗi dòng trắng ăn một lỗi "Thiếu: ...".
        const items: ShlxConfigItem[] = (result.data as string[][])
            .map((r) => ({
                terminalId: r[0],
                ddAccountNumber: r[1],
                terminalName: r[2],
                merchantId: r[3],
                province: r[4],
                branchCode: r[5],
                userName: r[6],
                // Rỗng -> null. Number('') là 0, mà 0 là ROLE_ID "có giá trị"
                // nên backend sẽ từ chối cả dòng.
                roleId: r[7] ? Number(r[7]) : null,
                fullName: r[8],
                email: r[9],
                mobile: r[10],
            }))
            .filter((x) => !laDongTrang(x));

        const loi = kiemTraLoShlx(items);

        this._terminals.set(items);
        this.loiDong.set(loi);

        const coUser = items.some((x) => !!x.userName);

        const cols: any[] = [
            { key: 'terminalId', label: 'TERMINAL_ID' },
            { key: 'ddAccountNumber', label: 'DD_ACCOUNT_NUMBER' },
            { key: 'terminalName', label: 'TERMINAL_NAME' },
            { key: 'merchantId', label: 'MERCHANT_ID' },
            { key: 'province', label: 'PROVINCE' },
            { key: 'branchCode', label: 'BRANCH_CODE' },
        ];

        // File không có phần user thì 5 cột rỗng chỉ làm nhiễu mắt.
        if (coUser) {
            cols.push(
                { key: 'userName', label: 'USER_NAME' },
                { key: 'roleId', label: 'ROLE_ID' },
                { key: 'fullName', label: 'FULLNAME' },
                { key: 'email', label: 'EMAIL' },
                { key: 'mobile', label: 'MOBILE' }
            );
        }

        this.columns.set(cols);
        this.dataSource.set(items);

        if (daDem.size) {
            ToastNotify(
                `${daDem.size} BRANCH_CODE đã khôi phục số 0 bị Excel cắt mất. Đối chiếu file gốc trước khi gửi.`,
                'warning'
            );
        }
    }

    /**
     * Hộp thoại lỗi — dùng chung một kiểu với luồng kết quả sát hạch.
     *
     * Lưới chỉ hiện dữ liệu, không hiện lỗi: một cột LỖI dài sẽ đẩy 11 cột kia
     * ra khỏi màn hình, mà lỗi là thứ đọc một lần rồi đi sửa file chứ không phải
     * thứ nhìn suốt.
     *
     * `dong` tính theo DÒNG EXCEL: chỉ số 0 ứng với dòng 2, vì dòng 1 là tiêu đề.
     */
    private _hienLoi(tieuDe: string, dong: { dong: number; lyDo: string }[]) {
        this._hienHop(
            tieuDe,
            dong.map((x) => `[Dòng ${x.dong}]: ${x.lyDo}`),
            'error'
        );
    }

    /**
     * Hộp thoại chung cho cả thành công lẫn thất bại.
     *
     * Thành công cũng dùng hộp thoại chứ không dùng toast: toast tự tắt sau vài
     * giây, mà sau một lô có thể vừa tạo hàng chục user với một mật khẩu phải
     * chép lại. Người dùng cần thời gian đọc và tự bấm đóng.
     */
    private _hienHop(
        tieuDe: string,
        dong: string[],
        // FuseConfirmationConfig khai color là union, không phải string:
        // 'accent' | 'basic' | 'error' | 'info' | 'primary' | 'success' | 'warn' | 'warning'
        mau: 'error' | 'success' | 'warning' | 'info'
    ) {
        this.fuseDialog.open({
            title: tieuDe,
            message: dong.join('<br>'),
            icon: { color: mau },
            actions: { cancel: { show: false }, confirm: { label: 'Xác nhận' } },
        });
    }

    // ── Cập nhật: rẽ theo loại file ────────────────────────────────────────

    async importData() {
        if (this.laFileTerminal()) {
            await this._guiLoTerminal();
            return;
        }

        await this._guiKetQua();
    }

    private async _guiLoTerminal() {
        const items = this._terminals();

        if (!items.length) {
            this._hienHop('Chưa có dữ liệu', ['Chọn file Excel rồi bấm Đọc dữ liệu trước.'], 'error');
            return;
        }

        // Chặn khi còn dòng hỏng: ACQHUB ghi cấu hình ở hệ thống ngoài, không
        // rollback được, nên thả lên rồi sửa sau thì đã muộn.
        if (this.loiDong().size) {
            this._hienLoi(
                `Có ${this.loiDong().size} dòng chưa hợp lệ`,
                [...this.loiDong()].map(([i, lyDo]) => ({
                    dong: i + 2,
                    lyDo,
                }))
            );
            return;
        }

        this.dangGui.set(true);

        try {
            const kq = await this._shlx.guiLo(items);

            if (!kq.loiTerminal.size && !kq.loiUser.size) {
                const dong = [`Đã cài ${kq.terminalXong}/${kq.daGui} terminal.`];

                if (kq.userDaTao) {
                    dong.push(
                        `Tạo mới ${kq.userDaTao} user portal.`,
                        `Mật khẩu ban đầu: <b>${kq.matKhauBanDau}</b>`,
                        'Mật khẩu này giống nhau cho mọi user tạo trong hôm nay — nhắc người dùng đổi ngay lần đăng nhập đầu.'
                    );
                } else {
                    dong.push('Không tạo user nào — file không có cột USER_NAME.');
                }

                this._hienHop('Cập nhật thành công', dong, 'success');

                this._terminals.set([]);
                this.dataSource.set([]);
                return;
            }

            // terminalId -> số dòng Excel, để hộp thoại chỉ được đúng dòng cần sửa.
            const soDong = new Map<string, number>(
                items.map((x, i) => [x.terminalId, i + 2])
            );

            const lyDo: { dong: number; lyDo: string }[] = [];

            for (const [tid, msg] of kq.loiTerminal) {
                lyDo.push({
                    dong: soDong.get(tid) ?? 0,
                    lyDo: `${tid} — ${msg}`,
                });
            }

            // Nhóm này KHÁC HẲN nhóm trên: terminal đã cài xong rồi, gửi lại chỉ
            // nhận "already exist". Phải nói rõ ra, không thì người dùng bấm lại.
            for (const [tid, msg] of kq.loiUser) {
                lyDo.push({
                    dong: soDong.get(tid) ?? 0,
                    lyDo: `${tid} — ${msg} ĐỪNG gửi lại, hãy tạo user bằng màn hình quản lý người dùng.`,
                });
            }

            lyDo.sort((a, b) => a.dong - b.dong);

            this._hienLoi(
                `${kq.terminalXong}/${kq.daGui} terminal thành công, ${lyDo.length} dòng có vấn đề`,
                lyDo
            );

            // Chỉ giữ lại dòng ACQHUB từ chối — những dòng đó sửa rồi gửi lại
            // được. Dòng đã cài mà thiếu user thì bỏ khỏi lưới, vì để lại là mời
            // người dùng bấm Cập nhật lần nữa.
            const conLai = items.filter((x) =>
                kq.loiTerminal.has(x.terminalId)
            );

            this._terminals.set(conLai);
            this.loiDong.set(new Map());
            this.dataSource.set(conLai);
        } catch (e) {
            this._hienHop('Không gửi được', [GetErrorText(e as HttpErrorResponse)], 'error');
        } finally {
            this.dangGui.set(false);
        }
    }

    private async _guiKetQua() {
        const rawSource = this.dataGrid.dataSource;

        if (!(this.dataGrid.visible && rawSource && isArray(rawSource))) return;

        const source: string[][] = (rawSource as any[]).map((item) =>
            [undefined as any].concat(item).map((item) =>
                item === null || item === undefined
                    ? undefined
                    : item instanceof Date
                      ? moment(item).format('DD/MM/YYYY')
                      : String(item)
            )
        );

        const maxItemPerApi = environment.maxItemPerApi;

        const totalCount = source.length;
        const total = Math.ceil(totalCount / maxItemPerApi);
        let succeed = 0;
        let error = 0;
        let desc = '';
        const batchNo = Math.trunc(Date.now() / 60000);

        for (let index = 0; index < total; index++) {
            const startIndex = index * maxItemPerApi;
            const endIndex = (index + 1) * maxItemPerApi;

            const data = source.slice(startIndex, endIndex);

            const payload: ImportShlxPayload = {
                batchNo,
                index: index + 1,
                total,
                totalCount,
                dataCount: data.length,
                data,
            };

            await lastValueFrom(
                this._httpClient.post<ImportShlxResponse>(
                    environment.mainEndpoint + url.bca.shlx + '?_=' + Date.now(),
                    payload
                )
            )
                .then((result: ImportShlxResponse) => {
                    succeed += result.success;
                    error += result.error;
                    if (desc == '') desc = result.desc;
                })
                .catch((err: HttpErrorResponse) => {
                    const msg = GetErrorText(err);
                    ToastNotify(msg, 'error');
                    throw new Error(msg);
                });
        }

        await this._requestService
            .put(environment.mainEndpoint + url.bca.shlx + '?_=' + Date.now(), {
                batchNo,
                total: succeed,
            })
            .catch((err: HttpErrorResponse) => {
                const msg = GetErrorText(err);
                ToastNotify(msg, 'error');
                throw new Error(msg);
            });

        let finalMsg = `Thành công ${succeed}/${totalCount}`;
        if (error > 0) finalMsg += `. Lỗi định dạng ${error}`;
        if (desc != '') finalMsg += `. Lỗi ${desc}`;

        ToastNotify(finalMsg);
    }
}
