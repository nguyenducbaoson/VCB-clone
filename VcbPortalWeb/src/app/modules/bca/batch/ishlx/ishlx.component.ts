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
import { ShlxConfigComponent } from 'app/modules/acqhub/shlx-config/ishlx-config.component';
import { RequestService } from 'app/services/request.service';
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

// ─────────────────────────────────────────────────────────────────────────────
// FILE KHUNG — dựng lại từ ảnh chụp màn hình của solution thật. ĐỪNG chép đè.
//
// CHÉP NGUYÊN VĂN từ ảnh: selectSheet(), importData(), onFileSelected(),
// openShlxConfig(), readFile(), setDefault(), ngAfterViewInit(), và toàn bộ HTML.
//
// TÔI TỰ VIẾT, ảnh bị che: khối import ở đầu file, decorator @Component,
// @ViewChild, khai báo selectControl, hai interface ImportShlxPayload/Response.
// ─────────────────────────────────────────────────────────────────────────────
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

    constructor(
        private _requestService: RequestService,
        private _httpClient: HttpClient,
        private dialog: MatDialog,
        private fuseDialog: FuseConfirmationService
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

        const file: File = e.target.files[0];

        if (file) this.worksheet.load(file);
    }

    openShlxConfig(): void {
        this.dialog.open(ShlxConfigComponent, {
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

        const result = this.worksheet.get(sheetSelect.id, tableConfig);

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

    async importData() {
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
