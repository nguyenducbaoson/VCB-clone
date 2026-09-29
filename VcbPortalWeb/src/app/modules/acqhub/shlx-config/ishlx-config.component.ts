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
        const tableConfig = ezTable([
            ez
                .preprocess(asText, ez.string().required())
                .meta({ label: 'TERMINAL_ID' })
                .onError({ type: 'throw' }),
            ez
                .preprocess(asText, ez.string().required())
                .meta({ label: 'DD_ACCOUNT_NUMBER' })
                .onError({ type: 'throw' }),
            ez
                .preprocess(asText, ez.string().required())
                .meta({ label: 'TERMINAL_NAME' })
                .onError({ type: 'throw' }),
            ez
                .preprocess(asText, ez.string().required())
                .meta({ label: 'MERCHANT_ID' })
                .onError({ type: 'throw' }),
            ez
                .preprocess(asText, ez.string().required())
                .meta({ label: 'PROVINCE' })
                .onError({ type: 'throw' }),
            ez
                .preprocess(asBranchCode, ez.string().required())
                .meta({ label: 'BRANCH_CODE' })
                .onError({ type: 'throw' }),
        ])
            .range(2, 10_000)
            .asArray();

        const result = this.worksheet.get(sheet.id, tableConfig);

        // Một ô trống là KHÔNG nạp dòng nào cả: gửi nửa file lên rồi mới phát hiện
        // thiếu thì khó lần ra cái nào đã gửi.
        if (result.issues.length) {
            this.rows.set([]);
            this._fuseDialog.open({
                title: `${result.issues.length} errors found`,
                message: result.issues
                    .map(
                        (item) =>
                            `[Row ${item.path[0]}, Col ${item.path[1]}]: ${item.message}`
                    )
                    .join('<br>'),
                icon: { color: 'error' },
                actions: { cancel: { show: false }, confirm: { label: 'OK' } },
            });
            return;
        }

        const items: ShlxConfigItem[] = (result.data as string[][]).map((r) => ({
            terminalId: r[0],
            ddAccountNumber: r[1],
            terminalName: r[2],
            merchantId: r[3],
            province: r[4],
            branchCode: r[5],
        }));

        this.rows.set(items);

        this.paddedBranchCodes.set(
            items
                .filter((_, i) => paddedRows.has(i))
                .map((x) => `${x.terminalId} → ${x.branchCode}`)
        );
    }

    onRowRemoved(e: { data: ShlxConfigItem }): void {
        this.rows.update((list) => list.filter((x) => x !== e.data));
    }

    async submitBatch(): Promise<void> {
        const items = this.rows();
        if (!items.length) {
            ToastNotify('Chưa có dữ liệu. Hãy chọn file Excel trước.', 'error');
            return;
        }

        await this._send(items);
    }

    private async _send(items: ShlxConfigItem[]): Promise<void> {
        this.submitting.set(true);

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
    // results[].success của từng cái. Dòng hỏng ở lại lưới để gửi lại, dòng thành
    // công biến mất.
    private _report(sent: number, results: ShlxConfigResult[]): void {
        const failed = results.filter((r) => !r.success);
        const ok = results.length - failed.length;

        if (!failed.length) {
            ToastNotify(`Installed ${ok} of ${sent} terminals.`);
            this._dialogRef.close(true);
            return;
        }

        this._fuseDialog
            .open({
                title: `${ok} of ${sent} succeeded, ${failed.length} failed`,
                message: failed
                    .map((r) => `${r.terminal_id}: [${r.result_code}] ${r.result_message}`)
                    .join('<br>'),
                icon: { color: 'warning' },
                actions: { cancel: { show: false }, confirm: { label: 'OK' } },
            })
            .afterClosed()
            .subscribe(() => {
                const failedIds = new Set(failed.map((r) => r.terminal_id));
                this.rows.update((list) =>
                    list.filter((x) => failedIds.has(x.terminalId))
                );
            });
    }

    cancel(): void {
        this._dialogRef.close(false);
    }
}
