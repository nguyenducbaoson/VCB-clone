import { HttpErrorResponse } from '@angular/common/http';
import {
    ChangeDetectionStrategy,
    Component,
    computed,
    effect,
    inject,
    signal,
    untracked,
    ViewEncapsulation,
} from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogRef } from '@angular/material/dialog';
import { MatDividerModule } from '@angular/material/divider';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
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
    imports: [
        ReactiveFormsModule,
        MatButtonModule,
        MatDividerModule,
        MatFormFieldModule,
        MatIconModule,
        MatInputModule,
        DxDataGridModule,
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
    submitting = signal<boolean>(false);

    isBatch = computed(() => this.rows().length > 0);

    form = new FormGroup({
        merchantId: new FormControl<string>('', Validators.required),
        terminalId: new FormControl<string>('', Validators.required),
        terminalName: new FormControl<string>('', Validators.required),
        ddAccountNumber: new FormControl<string>('', Validators.required),
        branchCode: new FormControl<string>('', Validators.required),
        province: new FormControl<string>('', Validators.required),
    });

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

        const paddedRows = new Set<number>();
        let branchRow = 0;

        const asBranchCode = (v: unknown) => {
            const row = branchRow++;
            const s = asText(v);
            if (typeof v !== 'number' || s.length >= 5) return s;
            paddedRows.add(row);
            return s.padStart(5, '0');
        };

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

    clearBatch(): void {
        this.rows.set([]);
        this.paddedBranchCodes.set([]);
        this.fileName.set('');
    }

    async submitSingle(): Promise<void> {
        if (this.form.invalid) {
            this.form.markAllAsTouched();
            ToastNotify('Please fill in all required fields.', 'error');
            return;
        }

        const item = this.form.getRawValue() as ShlxConfigItem;
        await this._send([item]);
    }

    async submitBatch(): Promise<void> {
        const items = this.rows();
        if (!items.length) return;

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
                    environment.mainEndpoint + 'acqh/shlx/cfg?_=' + Date.now(),
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
