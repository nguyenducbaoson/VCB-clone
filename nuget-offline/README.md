# nuget-offline

Goi NuGet cho project test, de restore tren may KHONG CO INTERNET.

## Dung nhu the nao

### Cach 1 - them package source trong Visual Studio (khuyen dung)

1. Tools -> Options -> NuGet Package Manager -> Package Sources
2. Bam dau + , dien:
   - Name:   offline
   - Source: duong dan tuyet doi toi thu muc nay
     vi du D:\src\VCB-clone\nuget-offline
3. OK, roi chuot phai Solution -> Restore NuGet Packages

Cach nay khong dung toi file cau hinh nao cua solution.

### Cach 2 - NuGet.config canh file .sln

    <?xml version="1.0" encoding="utf-8"?>
    <configuration>
      <packageSources>
        <add key="offline" value="nuget-offline" />
      </packageSources>
    </configuration>

Duong dan tinh tuong doi so voi file NuGet.config.

## Kiem tra da an chua

Mo lai project test, loi CS0246 'FactAttribute' phai bien mat, va
Test Explorer thay duoc cac test.

## Danh sach goi

Day la toan bo cay phu thuoc cua 3 package ma template xUnit dat vao:
Microsoft.NET.Test.Sdk 17.14.1, xunit 2.9.3, xunit.runner.visualstudio 3.1.4.

Khong co goi nao cho Oracle, EF hay thu vien mock - bo test chi dung
thu vien chuan cua .NET.

## Khi nao phai cap nhat thu muc nay

Doi version package trong .csproj, hoac them package moi. Luc do chay tren
may CO internet:

    dotnet restore ApiTests/ApiTests.csproj --packages ./_tmp
    copy toan bo *.nupkg trong ./_tmp vao thu muc nay
