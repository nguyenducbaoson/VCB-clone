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
    ShlxUserResult,
} from 'app/interfaces/shlx-config.interface';
import { RequestService } from 'app/services/request.service';
import { docO } from 'app/services/shlx-batch.service';
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
        .shlx-config-dialog .dx-data-row.shlx-row-canh-bao > td {
            background-color: rgb(254 243 199);
            color: rgb(120 53 15);
        }
        .dark .shlx-config-dialog .dx-data-row.shlx-row-canh-bao > td {
            background-color: rgb(120 53 15 / 0.35);
            color: rgb(253 230 138);
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

    /// MOBILE bi Excel an mat so 0 dau (con 9 chu so). Backend PadLeft lai duoc,
    /// nen day la THONG BAO chu khong phai loi — liet ke de doi chieu file goc.
    paddedMobiles = signal<string[]>([]);

    /// Chi so dong -> thieu cot nao. Loi cua FILE: chan khong cho gui.
    rowErrors = signal<Map<number, string>>(new Map());

    /// terminalId -> ly do ACQHUB tu choi. Loi cua LAN GUI: hien ra nhung VAN
    /// cho gui lai, vi sua xong hoac goi lai la co the qua.
    apiErrors = signal<Map<string, string>>(new Map());

    /// terminalId -> vi sao KHONG tao duoc user portal, du ACQHUB da cai xong.
    /// KHAC HAN hai loai tren: terminal DA CAI ROI, khong duoc gui lai.
    userErrors = signal<Map<string, string>>(new Map());

    /// So user vua tao, de bao cao cuoi cung.
    userCreated = signal<number>(0);

    submitting = signal<boolean>(false);

    /// File không có phần user thì 5 cột rỗng chỉ làm nhiễu mắt — ẩn đi.
    coCotUser = computed(() => this.rows().some((x) => !!x.userName));

    /// Bao nhiêu dòng sẽ tạo user. Nói thẳng con số ra, vì "có tạo user không"
    /// là thứ người dùng không nhìn được từ lưới khi file có 200 dòng.
    soDongTaoUser = computed(() => this.rows().filter((x) => !!x.userName).length);

    /// Tich vao = hien them 5 o USER_NAME/ROLE_ID/FULLNAME/EMAIL/MOBILE trong form
    /// nhap don. BO TICH = chi cai terminal o ACQHUB, KHONG tao user portal.
    ///
    /// Form nhap don luon hien, o tich khong bat/tat form — no chi bat/tat phan user.
    themUser = signal<boolean>(false);

    form = signal<ShlxConfigItem>(ShlxConfigComponent.formRong());

    /// Loi cua form nhap tay. Rieng khoi rowErrors vi khong co chi so dong.
    formLoi = signal<string[]>([]);

    private static formRong(): ShlxConfigItem {
        return {
            terminalId: '',
            ddAccountNumber: '',
            terminalName: '',
            merchantId: '',
            province: '',
            branchCode: '',
            userName: '',

            // PHẢI là null, KHÔNG phải 28.
            //
            // Ô "User" lúc mở dialog chưa tích, nên phần user coi như trống. Đặt
            // sẵn 28 ở đây thì `coDuLieuUser` trong _kiemTraGiaTri thấy roleId
            // khác null, kết luận "có dữ liệu user mà thiếu USER_NAME", và chặn
            // MỌI lần gửi đơn — kể cả khi 6 ô terminal đã điền đủ. 28 chỉ được
            // điền vào lúc tích ô User, trong doiCheDo().
            roleId: null,

            fullName: '',
            email: '',
            mobile: '',
        };
    }

    /// Doi mot o cua form. Dung signal thay ngModel de khong phai keo FormsModule
    /// vao mot component von chi co mot form duy nhat.
    capNhatForm(ten: keyof ShlxConfigItem, e: Event): void {
        const v = (e.target as HTMLInputElement).value;

        this.form.update((f) => ({
            ...f,
            [ten]: ten === 'roleId' ? (v ? Number(v) : null) : v,
        }));
    }

    doiCheDo(e: Event): void {
        const bat = (e.target as HTMLInputElement).checked;
        this.themUser.set(bat);
        this.formLoi.set([]);

        // Bỏ tích thì XOÁ luôn dữ liệu user đã gõ. Giữ lại là gửi đi thứ người dùng
        // đã cố ý ẩn đi — họ không nhìn thấy nó nữa nhưng nó vẫn tạo user.
        //
        // roleId về null chứ không về 28: gửi lên một role trong khi không yêu cầu
        // tạo user thì payload nói một đằng, ý định một nẻo. Tích lại thì 28 trở về
        // làm giá trị mặc định cho tiện gõ.
        this.form.update((f) =>
            bat
                ? { ...f, roleId: f.roleId ?? 28 }
                : {
                      ...f,
                      userName: '',
                      roleId: null,
                      fullName: '',
                      email: '',
                      mobile: '',
                  }
        );
    }

    /// Gui MOT terminal nhap tay. Duong nay doc lap voi Excel — Excel van la luong
    /// hang loat, cai nay cho truong hop cai le mot cai.
    async guiMotDong(): Promise<void> {
        const x = this.form();

        const loi = this._kiemTraGiaTri(x, this.themUser());

        if (loi.length) {
            this.formLoi.set(loi);
            return;
        }

        this.formLoi.set([]);

        // KHÔNG đổ vào rows(). Lưới là chỗ xem trước file Excel; một dòng gõ tay
        // thì người dùng đang nhìn thẳng vào nó trong form, dựng thêm một bảng
        // một dòng ngay dưới chỉ là nói lại thứ họ vừa gõ. Lỗi trả về hiện bằng
        // chữ ở khối formLoi.
        await this._send([x], true);
    }

    /// Mat khau ban dau backend cap cho user moi. Cong thuc co dinh trong
    /// ApiCommon.CreateUsers, khong phai bi mat — nguoi cai can biet de bao lai.
    matKhauBanDau = `Shlx@${this._yyMMdd()}`;

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
        this.paddedMobiles.set([]);
        this.rowErrors.set(new Map());
        this.apiErrors.set(new Map());
        this.userErrors.set(new Map());
        this.userCreated.set(0);

        const file: File = e.target.files[0];
        this.fileName.set(file.name);
        this.worksheet.load(file);
    }

    selectSheet(): void {
        const sheet = this.worksheet
            .sheetList()
            .find((s) => s.id === this.sheetControl.value);

        if (!sheet) return;

        // docO, khong phai String(): o Excel co the la hyperlink, rich text hay
        // cong thuc — String() cua chung ra '[object Object]'.
        const asText = docO;

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
        // lỗi. Đổi thứ tự cột trong file là dữ liệu vào sai ô mà không lỗi nào báo.
        //
        // CẢ 11 CỘT ĐỀU ĐỌC BẰNG asText, kể cả ROLE_ID vốn là số trong Excel. Đổi
        // sang số ở đây thì ô trống thành 0, mà 0 không phải "bỏ trống" — backend
        // sẽ coi đó là ROLE_ID hợp lệ rồi từ chối cả dòng. Chuyển kiểu ở bước map
        // bên dưới, nơi phân biệt được rỗng với số.
        //
        // KHÔNG dùng .required() ở bất kỳ cột nào — KỂ CẢ 6 cột bắt buộc.
        //
        // Đã đo trên `ez` thật: một ô required trượt thì CẢ DÒNG bị loại khỏi
        // result.data, `onError({type:'skip_cell'})` không giữ nó lại. Nghĩa là
        // dòng thiếu TERMINAL_ID sẽ biến mất khỏi lưới thay vì hiện đỏ, và người
        // dùng chỉ thấy "file không có dòng nào" mà không biết dòng nào hỏng.
        //
        // Nên để `ez` nạp mọi dòng, còn bắt buộc/không bắt buộc thì
        // _kiemTraGiaTri quyết — nó đã liệt kê sẵn tên cột thiếu.
        const cot = (label: string) =>
            ez.preprocess(asText, ez.string()).meta({ label });

        const tableConfig = ezTable([
            cot('TERMINAL_ID'),
            cot('DD_ACCOUNT_NUMBER'),
            cot('TERMINAL_NAME'),
            cot('MERCHANT_ID'),
            cot('PROVINCE'),
            ez.preprocess(asBranchCode, ez.string()).meta({ label: 'BRANCH_CODE' }),

            // ── 5 cột tạo user portal ────────────────────────────────────────
            // File không có phần này thì vẫn tải lên được, chỉ là không tạo user.
            cot('USER_NAME'),
            cot('ROLE_ID'),
            cot('FULLNAME'),
            cot('EMAIL'),
            cot('MOBILE'),
        ])
            .range(2, 10_000)
            .asArray();

        const result = this.worksheet.get(sheet.id, tableConfig);

        // BỎ DÒNG TRẮNG TRƯỚC KHI LÀM GÌ KHÁC.
        //
        // .range(2, 10_000) kéo về đúng 9999 dòng, phần lớn là trống. Trước đây
        // .required() tự loại chúng; bỏ required đi thì chúng vào hết, và mỗi dòng
        // trắng ăn một lỗi "Thiếu: TERMINAL_ID, ..." — file một dòng hoá ra hàng
        // nghìn dòng đỏ.
        //
        // Dòng trắng = mọi ô đều rỗng. Dòng chỉ thiếu vài ô thì vẫn giữ, để
        // _kiemTraGiaTri tô đỏ và nói thiếu cột nào.
        const duLieu = (result.data as string[][]).filter((r) =>
            r.some((c) => String(c ?? '').trim() !== '')
        );

        const items: ShlxConfigItem[] = duLieu.map((r) => ({
            terminalId: r[0],
            ddAccountNumber: r[1],
            terminalName: r[2],
            merchantId: r[3],
            province: r[4],
            branchCode: r[5],

            userName: r[6],
            // Rỗng -> null, để backend dùng mặc định 28. Number('') là 0, không
            // phải NaN, nên phải kiểm chuỗi rỗng TRƯỚC khi gọi Number().
            roleId: r[7] ? Number(r[7]) : null,
            fullName: r[8],
            email: r[9],
            mobile: r[10],
        }));

        // Dòng thiếu ô nào thì ghi lại TÊN CỘT đó, không chỉ đánh dấu đỏ. Màu sắc
        // một mình không nói được thiếu gì, và người mù màu thì không thấy.
        const loi = new Map<number, string>();

        // USER_NAME trùng trong cùng một file: chỉ dòng đầu tạo được user, dòng sau
        // nhận "user đã tồn tại" và im lặng gắn vào terminal khác. Bắt ở đây vì sau
        // khi gửi thì ACQHUB đã cài xong, không lùi lại được.
        const demUserName = new Map<string, number>();

        items.forEach((x) => {
            const u = (x.userName || '').toUpperCase();
            if (u) demUserName.set(u, (demUserName.get(u) ?? 0) + 1);
        });

        const mobileDemSo = new Set<number>();

        items.forEach((x, i) => {
            // false: file Excel không bắt buộc có phần user. Dòng nào có
            // USER_NAME thì tạo user cho dòng đó, dòng nào không thì chỉ cài
            // terminal — quyết định theo TỪNG DÒNG, không theo cả file.
            const vanDe = this._kiemTraGiaTri(x, false);

            if ((x.mobile ?? '').replace(/\D/g, '').length === 9) {
                mobileDemSo.add(i);
            }

            if (
                x.userName &&
                (demUserName.get(x.userName.toUpperCase()) ?? 0) > 1
            ) {
                vanDe.push('USER_NAME trùng với dòng khác trong file');
            }

            if (vanDe.length) loi.set(i, vanDe.join('. '));
        });

        this.rowErrors.set(loi);
        this.rows.set(items);

        this.paddedBranchCodes.set(
            items
                .filter((_, i) => paddedRows.has(i))
                .map((x) => `${x.terminalId} → ${x.branchCode}`)
        );

        this.paddedMobiles.set(
            items
                .filter((_, i) => mobileDemSo.has(i))
                .map((x) => `${x.terminalId} → 0${(x.mobile ?? '').replace(/\D/g, '')}`)
        );
    }

    /// Luat kiem cho MOT ban ghi, dung chung cho ca Excel lan form nhap don —
    /// hai cho kiem khac nhau thi som muon lech nhau, va cho lech se la cho khong
    /// ai kiem.
    ///
    /// USER_NAME LA CONG TAC: co thi dong nay tao user, khong co thi chi cai
    /// terminal. 5 cot user KHONG bat buoc.
    ///
    /// batBuocUser = true chi dung cho form nhap don khi da tich o "Them thong tin
    /// user" — luc do bo trong USER_NAME la mau thuan voi chinh o tich.
    private _kiemTraGiaTri(x: ShlxConfigItem, batBuocUser: boolean): string[] {
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

        // KHÔNG tính roleId vào đây.
        //
        // Nó là trường duy nhất có giá trị MẶC ĐỊNH (28), nên nó nói lên ý định
        // của người viết code chứ không phải của người dùng. Tính nó vào thì form
        // vừa mở đã bị coi là "có dữ liệu user", và mọi lần gửi đơn đều bị đòi
        // USER_NAME — kể cả khi ô "User" chưa hề được tích.
        //
        // Bốn trường còn lại thì khác: có chữ trong đó nghĩa là ai đó đã gõ vào.
        const coDuLieuUser = !!(x.userName || x.fullName || x.email || x.mobile);

        if (!x.userName) {
            if (batBuocUser) {
                vanDe.push('Thiếu: USER_NAME');
            } else if (coDuLieuUser) {
                // Gõ EMAIL/MOBILE mà quên USER_NAME thì backend bỏ qua LẶNG LẼ:
                // terminal vẫn cài, user không có, không ai báo gì. Gần như chắc
                // chắn là gõ sót chứ không phải cố ý.
                vanDe.push('Có dữ liệu user nhưng thiếu USER_NAME');
            }

            return vanDe;
        }

        // ROLE_ID: backend chỉ nhận 28 và 29. Bắt ở đây thay vì để backend từ
        // chối, vì lúc backend từ chối thì ACQHUB đã cài terminal xong rồi —
        // dòng đó thành "đã cài mà không có user", không sửa được bằng gửi lại.
        if (x.roleId !== null) {
            if (!Number.isFinite(x.roleId)) {
                vanDe.push('ROLE_ID không phải số');
            } else if (x.roleId !== 28 && x.roleId !== 29) {
                vanDe.push(`ROLE_ID ${x.roleId} không phải role SHLX (28, 29)`);
            }
        }

        // EMAIL sai định dạng thì backend vẫn lưu — KeepSafe() chỉ lọc ký tự lạ
        // chứ không kiểm cấu trúc. Hậu quả chỉ lộ ra lúc user quên mật khẩu và
        // thư không tới, tức là hàng tháng sau.
        if (x.email && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(x.email)) {
            vanDe.push('EMAIL sai định dạng');
        }

        // MOBILE: backend làm KeepNumber().PadLeft(10,'0').Trunc(10).
        //   9 chữ số   -> Excel nuốt số 0 đầu, PadLeft trả lại đúng. Chỉ báo.
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

    /// DevExtreme goi cho TUNG dong luc ve.
    /// Do = phai sua truoc khi gui. Vang = da cai xong roi, chi thieu user portal.
    onRowPrepared(e: any): void {
        if (e.rowType !== 'data') return;

        if (this.userErrors().get(e.data?.terminalId)) {
            e.rowElement.classList.add('shlx-row-canh-bao');
            return;
        }

        if (this.loiCuaDong(e.data)) e.rowElement.classList.add('shlx-row-loi');
    }

    /// Ly do hien o cot LOI. Gop ba nguon: thieu du lieu trong file, ACQHUB tu
    /// choi, va khong tao duoc user portal. Rong voi dong hop le.
    loiCuaDong = (item: ShlxConfigItem): string =>
        this.userErrors().get(item?.terminalId) ??
        this.apiErrors().get(item?.terminalId) ??
        this.rowErrors().get(this.rows().indexOf(item)) ??
        '';

    /// MOT nut Confirm batch duy nhat, dung nhu doc — khong co nut rieng cho form
    /// nhap don. Co file thi gui luoi, khong co thi gui form.
    ///
    /// Uu tien luoi chu khong phai form: file da nap la y dinh ro rang hon mot
    /// form co the con so du tu lan go truoc. Dong chu ngay tren nut noi thang
    /// dieu nay ra, de khong ai bam roi moi biet no gui cai khac.
    async submitBatch(): Promise<void> {
        // Điều kiện là CÓ FILE hay không, KHÔNG phải lưới có dòng hay không.
        //
        // guiMotDong() ghi dòng vừa gửi vào rows(), và khi gửi hỏng thì dòng đó ở
        // lại để hiện lý do. Lấy rows() làm điều kiện thì lần bấm thứ hai đi nhánh
        // lô, gửi lại đúng dòng cũ và bỏ qua thứ người dùng vừa sửa trong form —
        // sửa TERMINAL_ID xong bấm lại vẫn báo trùng y như cũ.
        if (!this.fileName()) {
            await this.guiMotDong();
            return;
        }

        const items = this.rows();

        if (!items.length) {
            ToastNotify('File không có dòng nào hợp lệ.', 'error');
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

    private async _send(
        items: ShlxConfigItem[],
          nhapDon = false
    ): Promise<void> {
        this.submitting.set(true);
        this.apiErrors.set(new Map());   // ket qua lan truoc khong con y nghia
        this.userErrors.set(new Map());

        const size = environment.maxItemPerApi;
        const total = Math.ceil(items.length / size);
        const results: ShlxConfigResult[] = [];
        const users: ShlxUserResult[] = [];

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
                users.push(...(res?.users ?? []));
            }
        } catch (error) {
            const msg = GetErrorText(error as HttpErrorResponse);
            ToastNotify(msg, 'error');
            this.submitting.set(false);
            return;
        }

        this.submitting.set(false);
        this._report(items, results, users, nhapDon);
    }

    // ACQHUB trả HTTP 200 kể cả khi vài terminal hỏng — trạng thái thật nằm ở
    // results[].success của từng cái. Dòng hỏng ở lại lưới kèm lý do, dòng thành
    // công biến mất.
    //
    // Không dùng hộp thoại liệt kê nữa: lỗi hiện ngay trên dòng tương ứng, người
    // dùng thấy được dòng nào hỏng vì sao mà không phải đối chiếu danh sách.
    private _report(
        daGui: ShlxConfigItem[],
        results: ShlxConfigResult[],
        users: ShlxUserResult[],
        nhapDon = false
    ): void {
        const sent = daGui.length;

        const failed = results.filter((r) => !r.success);
        const ok = results.length - failed.length;

        // Dòng CỐ Ý không tạo user (bỏ tích "Thêm thông tin user"). Backend vẫn trả
        // về "Thieu USER_NAME" cho những dòng này — đúng theo nó, nhưng với người
        // dùng thì đó là điều họ vừa chọn, không phải lỗi.
        const coYeuCauUser = new Set(
            daGui.filter((x) => x.userName).map((x) => x.terminalId)
        );

        // Terminal ĐÃ cài xong nhưng user portal thì không. Gửi lại KHÔNG sửa được
        // — ACQHUB sẽ báo "already exist" — nên phải tách hẳn khỏi nhóm lỗi trên.
        const userHong = users.filter(
            (u) => !u.created && coYeuCauUser.has(u.terminal_id)
        );

        this.userCreated.set(users.filter((u) => u.created).length);

        // ── Đường nhập đơn: chỉ nói lý do, không dựng lưới ────────────────────
        if (nhapDon) {
            const lyDo = [
                ...failed.map((r) => `[${r.result_code}] ${r.result_message}`),
                ...userHong.map(
                    (u) => `Đã cài terminal nhưng CHƯA có user: ${u.message}`
                ),
            ];

            if (!lyDo.length) {
                ToastNotify(
                    `Đã cài đặt terminal${this.userCreated() ? ' và tạo user' : ''}.`
                );
                this._dialogRef.close(true);
                return;
            }

            // Giữ nguyên form để người dùng sửa ngay chỗ sai rồi bấm lại.
            this.formLoi.set(lyDo);
            ToastNotify(lyDo[0], 'error');
            return;
        }

        this.apiErrors.set(
            new Map(
                failed.map((r) => [
                    r.terminal_id,
                    `[${r.result_code}] ${r.result_message}`,
                ])
            )
        );

        this.userErrors.set(
            new Map(
                userHong.map((u) => [
                    u.terminal_id,
                    `Đã cài terminal nhưng CHƯA có user: ${u.message}`,
                ])
            )
        );

        if (!failed.length && !userHong.length) {
            ToastNotify(
                `Đã cài đặt ${ok}/${sent} terminal và tạo ${this.userCreated()} user.`
            );
            this._dialogRef.close(true);
            return;
        }

        // ACQHUB từ chối terminal đã cài thay vì ghi đè. Tách riêng nhóm đó: tải
        // lại đúng file cũ thì mọi dòng đều "đã tồn tại", mà đó không phải hỏng.
        const daTonTai = failed.filter((r) =>
            /already exist/i.test(r.result_message ?? '')
        ).length;

        // Giữ lại dòng cần người dùng nhìn: terminal ACQHUB từ chối (gửi lại được)
        // và terminal đã cài nhưng thiếu user (gửi lại KHÔNG được, phải xử tay).
        const canXem = new Set([
            ...failed.map((r) => r.terminal_id),
            ...userHong.map((u) => u.terminal_id),
        ]);

        this.rows.update((list) => list.filter((x) => canXem.has(x.terminalId)));

        const phan = [`${ok}/${sent} terminal thành công`];
        if (daTonTai) phan.push(`${daTonTai} đã tồn tại`);
        if (failed.length - daTonTai) {
            phan.push(`${failed.length - daTonTai} lỗi`);
        }
        if (userHong.length) phan.push(`${userHong.length} chưa tạo được user`);

        ToastNotify(phan.join(', ') + '. Xem cột LỖI trong lưới.', 'warning');
    }

    cancel(): void {
        this._dialogRef.close(false);
    }

    /// Ngay hom nay dang yyMMdd, khop $"Shlx@{DateTime.Now:yyMMdd}" ben backend.
    private _yyMMdd(): string {
        const d = new Date();
        const p = (n: number) => String(n).padStart(2, '0');
        return `${p(d.getFullYear() % 100)}${p(d.getMonth() + 1)}${p(d.getDate())}`;
    }
}
