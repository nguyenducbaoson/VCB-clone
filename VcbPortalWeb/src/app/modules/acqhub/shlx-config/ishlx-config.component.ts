import { HttpErrorResponse } from '@angular/common/http';
import {
    ChangeDetectionStrategy,
    Component,
    effect,
    inject,
    signal,
    untracked,
    ViewEncapsulation,
} from '@angular/core';
import { FormControl } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { FuseConfirmationService } from '@fuse/services/confirmation';
import { GetErrorText, ToastNotify } from 'app/helpers/common.helper';
import {
    ShlxConfigItem,
    ShlxConfigPayload,
    ShlxConfigResponse,
    ShlxConfigResult,
} from 'app/interfaces/shlx-config.interface';
import { RequestService } from 'app/services/request.service';
import { ez, ezTable, injectWorkSheet } from 'app/shared/state/excel';
import { DxDataGridModule } from 'devextreme-angular';
import { environment } from 'environments/environment';

@Component({
    selector: 'ishlx-config',
    templateUrl: './ishlx-config.component.html',
    encapsulation: ViewEncapsulation.None,
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
    imports: [MatButtonModule, MatIconModule, DxDataGridModule],

    // ViewEncapsulation.None nen selector phai du dac trung de khong dinh sang
    // luoi khac trong trang.
    styles: [
        `
        .shlx-config-dialog .dx-data-row.shlx-row-loi > td {
            background-color: rgb(254 226 226);
            color: rgb(127 29 29);
        }
        .dark .shlx-config-dialog .dx-data-row.shlx-row-loi > td {
            background-color: rgb(127 29 29 / 0.35);
            color: rgb(254 202 202);
        }
        `,
    ],
})
export class ShlxConfigComponent {
    private _dialogRef = inject(MatDialogRef<ShlxConfigComponent>);
    private _fuseDialog = inject(FuseConfirmationService);
    private _requestService = inject(RequestService);

    worksheet = injectWorkSheet();

    sheetControl = new FormControl<number>(0);

    fileName = signal<string>('');
    rows = signal<ShlxConfigItem[]>([]);

    paddedBranchCodes = signal<string[]>([]);

    /// Chi so dong -> thieu cot nao. Loi cua FILE: chan khong cho gui.
    rowErrors = signal<Map<number, string>>(new Map());

    /// terminalId -> ly do ACQHUB tu choi. Loi cua LAN GUI: hien ra nhung VAN
    /// cho gui lai, vi sua xong hoac goi lai la co the qua.
    apiErrors = signal<Map<string, string>>(new Map());
    submitting = signal<boolean>(false);

    constructor() {
        effect(() => {
            const list = this.worksheet.sheetList();
            if (!list.length) return;
            untracked(() => {
                this.sheetControl.setValue(list[0].id);
                this.selectSheet();
            });
        });
    }

    onFileSelected(e: any): void {
        if (!e.target || !e.target.files || e.target.files.length <= 0) return;

        this.rows.set([]);
        this.paddedBranchCodes.set([]);
        this.rowErrors.set(new Map());
        this.apiErrors.set(new Map());

        const file: File = e.target.files[0];
        this.fileName.set(file.name);
        this.worksheet.load(file);
    }

    selectSheet(): void {
        const sheet = this.worksheet
            .sheetList()
            .find((s) => s.id === this.sheetControl.value);

        if (!sheet) return;

        const asText = (v: unknown) => String(v ?? '').trim();

        // Đệm số 0 cho BRANCH_CODE, nhưng ghi nhớ theo CHỈ SỐ DÒNG chứ không theo
        // giá trị: sau khi đệm thì '01400' gõ tay và '01400' vừa đệm giống hệt nhau,
        // so theo giá trị sẽ báo nhầm cả những dòng vốn đã đúng.
        const paddedRows = new Set<number>();
        let branchRow = 0;

        const asBranchCode = (v: unknown) => {
            const row = branchRow++;
            const s = asText(v);
            if (typeof v !== 'number' || s.length >= 5) return s;
            paddedRows.add(row);
            return s.padStart(5, '0');
        };

        // THỨ TỰ CỘT quyết định dữ liệu vào ô nào — label chỉ dùng để in thông báo
        // lỗi. Đổi thứ tự cột trong file là dữ liệu vào sai ô mà không lỗi nào báo,
        // vì cả 6 cột đều là chuỗi bắt buộc.
        //
        // onError: 'skip_cell' chứ KHÔNG phải 'throw' — ô hỏng thì để trống ô đó
        // nhưng vẫn nạp cả dòng. Có nạp thì mới tô được dòng lỗi trong lưới; 'throw'
        // làm cả file không vào, người dùng chỉ thấy một hộp thoại rồi lưới trống.
        const tableConfig = ezTable([
            ez
                .preprocess(asText, ez.string().required())
                .meta({ label: 'TERMINAL_ID' })
                .onError({ type: 'skip_cell' }),
            ez
                .preprocess(asText, ez.string().required())
                .meta({ label: 'DD_ACCOUNT_NUMBER' })
                .onError({ type: 'skip_cell' }),
            ez
                .preprocess(asText, ez.string().required())
                .meta({ label: 'TERMINAL_NAME' })
                .onError({ type: 'skip_cell' }),
            ez
                .preprocess(asText, ez.string().required())
                .meta({ label: 'MERCHANT_ID' })
                .onError({ type: 'skip_cell' }),
            ez
                .preprocess(asText, ez.string().required())
                .meta({ label: 'PROVINCE' })
                .onError({ type: 'skip_cell' }),
            ez
                .preprocess(asBranchCode, ez.string().required())
                .meta({ label: 'BRANCH_CODE' })
                .onError({ type: 'skip_cell' }),
        ])
            .range(2, 10_000)
            .asArray();

        const result = this.worksheet.get(sheet.id, tableConfig);

        const items: ShlxConfigItem[] = (result.data as string[][]).map((r) => ({
            terminalId: r[0],
            ddAccountNumber: r[1],
            terminalName: r[2],
            merchantId: r[3],
            province: r[4],
            branchCode: r[5],
        }));

        // Dòng thiếu ô nào thì ghi lại TÊN CỘT đó, không chỉ đánh dấu đỏ. Màu sắc
        // một mình không nói được thiếu gì, và người mù màu thì không thấy.
        const loi = new Map<number, string>();

        items.forEach((x, i) => {
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

            if (thieu.length) loi.set(i, 'Thiếu: ' + thieu.join(', '));
        });

        this.rowErrors.set(loi);
        this.rows.set(items);

        this.paddedBranchCodes.set(
            items
                .filter((_, i) => paddedRows.has(i))
                .map((x) => `${x.terminalId} → ${x.branchCode}`)
        );
    }

    /// DevExtreme goi cho TUNG dong luc ve. Them class de to do dong hong.
    onRowPrepared(e: any): void {
        if (e.rowType !== 'data') return;
        if (this.loiCuaDong(e.data)) e.rowElement.classList.add('shlx-row-loi');
    }

    /// Ly do hien o cot LOI. Gop ca hai nguon: thieu du lieu trong file, va
    /// ACQHUB tu choi. Rong voi dong hop le.
    loiCuaDong = (item: ShlxConfigItem): string =>
        this.apiErrors().get(item?.terminalId) ??
        this.rowErrors().get(this.rows().indexOf(item)) ??
        '';

    async submitBatch(): Promise<void> {
        const items = this.rows();
        if (!items.length) {
            ToastNotify('Chưa có dữ liệu. Hãy chọn file Excel trước.', 'error');
            return;
        }

        // Chặn gửi khi còn dòng hỏng: ACQHUB ghi cấu hình ở hệ thống ngoài, không
        // rollback được, nên thả lên rồi sửa sau thì đã muộn.
        if (this.rowErrors().size) {
            ToastNotify(
                `Còn ${this.rowErrors().size} dòng thiếu dữ liệu. Sửa file rồi tải lại.`,
                'error'
            );
            return;
        }

        await this._send(items);
    }

    private async _send(items: ShlxConfigItem[]): Promise<void> {
        this.submitting.set(true);
        this.apiErrors.set(new Map());   // ket qua lan truoc khong con y nghia

        const size = environment.maxItemPerApi;
        const total = Math.ceil(items.length / size);
        const results: ShlxConfigResult[] = [];

        try {
            for (let i = 0; i < total; i++) {
                const payload: ShlxConfigPayload = {
                    items: items.slice(i * size, (i + 1) * size),
                };

                const res = (await this._requestService.post(
                    environment.mainEndpoint + 'acqh/para/shlx?_=' + Date.now(),
                    payload
                )) as ShlxConfigResponse;

                results.push(...(res?.results ?? []));
            }
        } catch (error) {
            const msg = GetErrorText(error as HttpErrorResponse);
            ToastNotify(msg, 'error');
            this.submitting.set(false);
            return;
        }

        this.submitting.set(false);
        this._report(items.length, results);
    }

    // ACQHUB trả HTTP 200 kể cả khi vài terminal hỏng — trạng thái thật nằm ở
    // results[].success của từng cái. Dòng hỏng ở lại lưới kèm lý do, dòng thành
    // công biến mất.
    //
    // Không dùng hộp thoại liệt kê nữa: lỗi hiện ngay trên dòng tương ứng, người
    // dùng thấy được dòng nào hỏng vì sao mà không phải đối chiếu danh sách.
    private _report(sent: number, results: ShlxConfigResult[]): void {
        const failed = results.filter((r) => !r.success);
        const ok = results.length - failed.length;

        if (!failed.length) {
            ToastNotify(`Đã cài đặt ${ok}/${sent} terminal.`);
            this._dialogRef.close(true);
            return;
        }

        // ACQHUB từ chối terminal đã cài thay vì ghi đè. Tách riêng nhóm đó: tải
        // lại đúng file cũ thì mọi dòng đều "đã tồn tại", mà đó không phải hỏng.
        const daTonTai = failed.filter((r) =>
            /already exist/i.test(r.result_message ?? '')
        ).length;

        this.apiErrors.set(
            new Map(
                failed.map((r) => [
                    r.terminal_id,
                    `[${r.result_code}] ${r.result_message}`,
                ])
            )
        );

        const failedIds = new Set(failed.map((r) => r.terminal_id));
        this.rows.update((list) => list.filter((x) => failedIds.has(x.terminalId)));

        const phan = [`${ok}/${sent} thành công`];
        if (daTonTai) phan.push(`${daTonTai} đã tồn tại`);
        if (failed.length - daTonTai) {
            phan.push(`${failed.length - daTonTai} lỗi`);
        }

        ToastNotify(phan.join(', ') + '. Xem cột LỖI trong lưới.', 'warning');
    }

    cancel(): void {
        this._dialogRef.close(false);
    }
}
