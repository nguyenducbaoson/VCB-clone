import { NgTemplateOutlet } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
    ChangeDetectionStrategy,
    Component,
    inject,
    signal,
} from '@angular/core';
import {
    FormControl,
    FormGroup,
    ReactiveFormsModule,
    Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { GetErrorText, ToastNotify } from 'app/helpers/common.helper';
import {
    ShlxConfigItem,
    ShlxConfigPayload,
    ShlxConfigResponse,
} from 'app/interfaces/shlx-config.interface';
import { RequestService } from 'app/services/request.service';
import { kiemTraDongShlx } from 'app/services/shlx-batch.service';
import { environment } from 'environments/environment';

/**
 * Cài đặt MỘT terminal SHLX.
 *
 * KHÔNG đọc Excel nữa. Lô đi qua ImportShlxComponent — một chỗ đọc file là đủ;
 * hai chỗ nghĩa là hai bộ cấu hình ezTable phải sửa song song, và chỗ nào không
 * ai chạy thử sẽ là chỗ hỏng.
 */
@Component({
    selector: 'ishlx-config',
    templateUrl: './ishlx-config.component.html',
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
    imports: [
        NgTemplateOutlet,
        ReactiveFormsModule,
        MatButtonModule,
        MatCardModule,
        MatCheckboxModule,
        MatFormFieldModule,
        MatInputModule,
    ],
})
export class ShlxConfigComponent {
    private _dialogRef = inject(MatDialogRef<ShlxConfigComponent>);
    private _requestService = inject(RequestService);

    submitting = signal<boolean>(false);

    /// Tich vao = tao them user portal. Bo tich = chi cai terminal o ACQHUB.
    themUser = signal<boolean>(false);

    /// Ly do khong gui duoc. Hien ngay duoi form de sua tai cho.
    formLoi = signal<string[]>([]);

    /// Validators.required CHI o 6 truong ACQHUB. Phan user kiem bang
    /// kiemTraDongShlx — luat cua no doi theo o tich, ma FormGroup thi khong.
    form = new FormGroup({
        merchantId: new FormControl<string>('', Validators.required),
        terminalId: new FormControl<string>('', Validators.required),
        terminalName: new FormControl<string>('', Validators.required),
        ddAccountNumber: new FormControl<string>('', Validators.required),
        branchCode: new FormControl<string>('', Validators.required),
        province: new FormControl<string>('', Validators.required),

        userName: new FormControl<string>(''),
        roleId: new FormControl<number | null>(null),
        fullName: new FormControl<string>(''),
        email: new FormControl<string>(''),
        mobile: new FormControl<string>(''),
    });

    doiCheDo(bat: boolean): void {
        this.themUser.set(bat);
        this.formLoi.set([]);

        // Bỏ tích thì XOÁ dữ liệu user đã gõ. Giữ lại là gửi đi thứ người dùng đã
        // cố ý ẩn đi — họ không nhìn thấy nó nữa nhưng nó vẫn tạo user.
        //
        // roleId chỉ thành 28 khi tích. Để sẵn 28 lúc chưa tích thì payload khai
        // một role trong khi không hề yêu cầu tạo user.
        if (bat) {
            if (this.form.controls.roleId.value == null) {
                this.form.controls.roleId.setValue(28);
            }
            return;
        }

        this.form.patchValue({
            userName: '',
            roleId: null,
            fullName: '',
            email: '',
            mobile: '',
        });
    }

    private _item(): ShlxConfigItem {
        const v = this.form.getRawValue();

        return {
            merchantId: v.merchantId ?? '',
            terminalId: v.terminalId ?? '',
            terminalName: v.terminalName ?? '',
            ddAccountNumber: v.ddAccountNumber ?? '',
            branchCode: v.branchCode ?? '',
            province: v.province ?? '',
            userName: v.userName ?? '',
            roleId: v.roleId ?? null,
            fullName: v.fullName ?? '',
            email: v.email ?? '',
            mobile: v.mobile ?? '',
        };
    }

    async onSubmit(): Promise<void> {
        const x = this._item();

        const loi = kiemTraDongShlx(x);

        // Tích ô user mà bỏ trống USER_NAME là mâu thuẫn với chính ô tích.
        if (this.themUser() && !x.userName) loi.push('Thiếu: USER_NAME');

        if (loi.length) {
            this.formLoi.set(loi);
            return;
        }

        this.formLoi.set([]);
        this.submitting.set(true);

        try {
            const payload: ShlxConfigPayload = { items: [x] };

            const res = (await this._requestService.post(
                environment.mainEndpoint + 'acqh/para/shlx?_=' + Date.now(),
                payload
            )) as ShlxConfigResponse;

            const hong = (res?.results ?? []).filter((r) => !r.success);

            // Dòng CỐ Ý không tạo user: backend vẫn trả "Thieu USER_NAME" cho nó
            // — đúng theo backend, nhưng đó là điều người dùng vừa chọn.
            const userHong = x.userName
                ? (res?.users ?? []).filter((u) => !u.created)
                : [];

            if (hong.length || userHong.length) {
                this.formLoi.set([
                    ...hong.map((r) => `[${r.result_code}] ${r.result_message}`),
                    ...userHong.map(
                        (u) =>
                            `Đã cài terminal nhưng CHƯA có user: ${u.message} ` +
                            'Đừng gửi lại, hãy tạo user bằng màn hình quản lý người dùng.'
                    ),
                ]);
                return;
            }

            const daTao = (res?.users ?? []).filter((u) => u.created).length;

            ToastNotify(
                daTao ? 'Đã cài terminal và tạo user portal.' : 'Đã cài terminal.'
            );

            this._dialogRef.close(true);
        } catch (e) {
            this.formLoi.set([GetErrorText(e as HttpErrorResponse)]);
        } finally {
            this.submitting.set(false);
        }
    }

    cancel(): void {
        this._dialogRef.close(false);
    }
}
