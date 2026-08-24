--------------------------------------------------------------------------------
-- BỘ TEST cho sync_mp_app_user_partner_status.sql
--
-- Toàn bộ file này chỉ có SELECT, KHÔNG ghi gì vào DB -> chạy an toàn trên UAT.
-- Khối reg/agg/resolved/src dưới đây là BẢN SAO NGUYÊN VĂN của script update,
-- chỉ thay 2 bảng nguồn bằng dữ liệu inline. Sửa logic ở script update thì phải
-- copy sang đây, nếu không test mất giá trị.
--
-- BLOCK A: 26 case logic, có cột KET_QUA = PASS / >>> FAIL
-- BLOCK B: đếm tổng PASS/FAIL (phải là 0 FAIL)
-- BLOCK C: chạy trên DỮ LIỆU THẬT UAT, in ra dòng nguồn + giá trị hiện tại +
--          giá trị sẽ ghi, để đối chiếu bằng mắt trước khi update
-- BLOCK D: soi các giá trị STATUS / PARTNER lạ đang có trong UAT
--------------------------------------------------------------------------------


-- =============================================================================
-- BLOCK A - Test logic bằng dữ liệu giả
-- =============================================================================
WITH t_users AS (
    --        USERNAME          EXP_PP (PHONEPOS)      EXP_VA (VISAACCEPT)   MÔ TẢ
    SELECT CAST('T01' AS VARCHAR2(30)) AS USERNAME, CAST(0 AS NUMBER) AS EXP_PP, CAST(NULL AS NUMBER) AS EXP_VA, CAST('PP {0} -> 0 (Da dang ky); VA khong co dong nao -> NULL' AS VARCHAR2(200)) AS MO_TA FROM DUAL UNION ALL
    SELECT 'T02', CAST(2    AS NUMBER), CAST(NULL AS NUMBER), 'PP {2} -> 2 (Kich hoat)'                              FROM DUAL UNION ALL
    SELECT 'T03', CAST(2    AS NUMBER), CAST(NULL AS NUMBER), 'PP {3} -> 2'                                          FROM DUAL UNION ALL
    SELECT 'T04', CAST(2    AS NUMBER), CAST(NULL AS NUMBER), 'PP {4} -> 2'                                          FROM DUAL UNION ALL
    SELECT 'T05', CAST(2    AS NUMBER), CAST(NULL AS NUMBER), 'PP {5} -> 2'                                          FROM DUAL UNION ALL
    SELECT 'T06', CAST(2    AS NUMBER), CAST(NULL AS NUMBER), 'PP {6} -> 2'                                          FROM DUAL UNION ALL
    SELECT 'T07', CAST(7    AS NUMBER), CAST(NULL AS NUMBER), 'PP {7} -> 7 (Huy)'                                    FROM DUAL UNION ALL
    SELECT 'T08', CAST(7    AS NUMBER), CAST(NULL AS NUMBER), 'PP {7,7} -> 7'                                        FROM DUAL UNION ALL
    SELECT 'T09', CAST(0    AS NUMBER), CAST(NULL AS NUMBER), 'PP {0,7} -> 0'                                        FROM DUAL UNION ALL
    SELECT 'T10', CAST(0    AS NUMBER), CAST(NULL AS NUMBER), 'PP {7,7,0} -> 0'                                      FROM DUAL UNION ALL
    SELECT 'T11', CAST(2    AS NUMBER), CAST(NULL AS NUMBER), 'PP {2,7} -> 2  <<< THU TU NHANH: 2 thang 7'           FROM DUAL UNION ALL
    SELECT 'T12', CAST(2    AS NUMBER), CAST(NULL AS NUMBER), 'PP {7,0,3} -> 2'                                      FROM DUAL UNION ALL
    SELECT 'T13', CAST(NULL AS NUMBER), CAST(NULL AS NUMBER), 'PP {1} -> NULL (1 khong thuoc nhanh nao)'             FROM DUAL UNION ALL
    SELECT 'T14', CAST(NULL AS NUMBER), CAST(NULL AS NUMBER), 'PP {8} -> NULL'                                       FROM DUAL UNION ALL
    SELECT 'T15', CAST(NULL AS NUMBER), CAST(NULL AS NUMBER), 'PP {0,1} -> NULL (co 0 nhung lan 1)'                  FROM DUAL UNION ALL
    SELECT 'T16', CAST(NULL AS NUMBER), CAST(NULL AS NUMBER), 'PP {7,1} -> NULL (khong phai toan 7)'                 FROM DUAL UNION ALL
    SELECT 'T17', CAST(NULL AS NUMBER), CAST(NULL AS NUMBER), 'PP {X} khong phai so -> NULL'                         FROM DUAL UNION ALL
    SELECT 'T18', CAST(NULL AS NUMBER), CAST(NULL AS NUMBER), 'PP {0,X} -> NULL (X pha vo "toan 0/7")'               FROM DUAL UNION ALL
    SELECT 'T19', CAST(2    AS NUMBER), CAST(NULL AS NUMBER), 'PP {2,X} -> 2 (nhanh 2..6 thang truoc)'               FROM DUAL UNION ALL
    SELECT 'T20', CAST(2    AS NUMBER), CAST(NULL AS NUMBER), 'PP {"  2 "} co khoang trang -> 2 (giong int.TryParse)' FROM DUAL UNION ALL
    SELECT 'T21', CAST(NULL AS NUMBER), CAST(2    AS NUMBER), 'VA {3,3,0} -> 2 (ca VATID001 that)'                   FROM DUAL UNION ALL
    SELECT 'T22', CAST(2    AS NUMBER), CAST(2    AS NUMBER), 'PP {2} va VA {3} doc lap nhau, khong lay nham'        FROM DUAL UNION ALL
    SELECT 'T23', CAST(7    AS NUMBER), CAST(0    AS NUMBER), 'PP {7} + VA {0}: 2 partner ra 2 gia tri khac nhau'    FROM DUAL UNION ALL
    SELECT 'T24', CAST(NULL AS NUMBER), CAST(NULL AS NUMBER), 'Khong co dong nao trong CARD_REG -> ca 2 NULL'        FROM DUAL UNION ALL
    SELECT 'T25', CAST(2    AS NUMBER), CAST(NULL AS NUMBER), 'PARTNER "phonepos" chu thuong -> van tinh'            FROM DUAL UNION ALL
    SELECT 'T26', CAST(0    AS NUMBER), CAST(NULL AS NUMBER), 'USERNAME lech hoa/thuong + khoang trang -> van khop'  FROM DUAL
),
t_reg AS (
    --        USERNAME      STATUS   PARTNER
    SELECT CAST('T01' AS VARCHAR2(100)) AS USERNAME, CAST('0' AS VARCHAR2(10)) AS STATUS, CAST('PHONEPOS' AS VARCHAR2(30)) AS PARTNER FROM DUAL UNION ALL
    SELECT 'T02', '2',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T03', '3',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T04', '4',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T05', '5',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T06', '6',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T07', '7',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T08', '7',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T08', '7',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T09', '0',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T09', '7',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T10', '7',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T10', '7',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T10', '0',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T11', '2',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T11', '7',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T12', '7',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T12', '0',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T12', '3',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T13', '1',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T14', '8',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T15', '0',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T15', '1',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T16', '7',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T16', '1',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T17', 'X',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T18', '0',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T18', 'X',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T19', '2',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T19', 'X',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T20', ' 2 ', 'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T21', '3',   'VISAACCEPT' FROM DUAL UNION ALL
    SELECT 'T21', '3',   'VISAACCEPT' FROM DUAL UNION ALL
    SELECT 'T21', '0',   'VISAACCEPT' FROM DUAL UNION ALL
    SELECT 'T22', '2',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T22', '3',   'VISAACCEPT' FROM DUAL UNION ALL
    SELECT 'T23', '7',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T23', '0',   'VISAACCEPT' FROM DUAL UNION ALL
    -- T24 co tinh khong co dong nao
    SELECT 'T25', '2',   'phonepos'   FROM DUAL UNION ALL
    SELECT ' t26 ', '0', 'PHONEPOS'   FROM DUAL
),
-- ---- tu day xuong la ban sao nguyen van cua script update ----------------
reg AS (
    SELECT UPPER(TRIM(r.USERNAME)) AS UNAME,
           UPPER(TRIM(r.PARTNER))  AS PARTNER,
           CASE WHEN REGEXP_LIKE(TRIM(r.STATUS), '^[+-]?[0-9]+$')
                THEN TO_NUMBER(TRIM(r.STATUS))
           END AS ST
    FROM   t_reg r
),
agg AS (
    SELECT UNAME,
           PARTNER,
           COUNT(*)                                       AS CNT,
           COUNT(CASE WHEN ST BETWEEN 2 AND 6 THEN 1 END) AS CNT_ACTIVE,
           COUNT(CASE WHEN ST = 7 THEN 1 END)             AS CNT_CANCEL,
           COUNT(CASE WHEN ST = 0 THEN 1 END)             AS CNT_REG
    FROM   reg
    GROUP  BY UNAME, PARTNER
),
resolved AS (
    SELECT UNAME,
           PARTNER,
           CASE
               WHEN CNT_ACTIVE > 0             THEN 2
               WHEN CNT_CANCEL = CNT           THEN 7
               WHEN CNT_REG + CNT_CANCEL = CNT THEN 0
               ELSE NULL
           END AS STATUS_VAL
    FROM   agg
),
-- -------------------------------------------------------------------------
src AS (
    SELECT u.USERNAME, u.MO_TA, u.EXP_PP, u.EXP_VA,
           MAX(CASE WHEN res.PARTNER = 'PHONEPOS'   THEN res.STATUS_VAL END) AS NEW_PP,
           MAX(CASE WHEN res.PARTNER = 'VISAACCEPT' THEN res.STATUS_VAL END) AS NEW_VA
    FROM   t_users u
           LEFT JOIN resolved res ON res.UNAME = UPPER(TRIM(u.USERNAME))
    GROUP  BY u.USERNAME, u.MO_TA, u.EXP_PP, u.EXP_VA
)
SELECT CASE WHEN DECODE(NEW_PP, EXP_PP, 1, 0) = 1
             AND DECODE(NEW_VA, EXP_VA, 1, 0) = 1
            THEN 'PASS' ELSE '>>> FAIL' END AS KET_QUA,
       USERNAME,
       NVL(TO_CHAR(EXP_PP), 'NULL') AS PP_MONG_DOI,
       NVL(TO_CHAR(NEW_PP), 'NULL') AS PP_THUC_TE,
       NVL(TO_CHAR(EXP_VA), 'NULL') AS VA_MONG_DOI,
       NVL(TO_CHAR(NEW_VA), 'NULL') AS VA_THUC_TE,
       MO_TA
FROM   src
ORDER  BY KET_QUA, USERNAME;   -- '>>> FAIL' sap truoc 'PASS'


-- =============================================================================
-- BLOCK B - Tong ket: SO_FAIL phai = 0
-- (chay lai y het BLOCK A, chi doi phan SELECT cuoi)
-- =============================================================================
WITH t_users AS (
    SELECT CAST('T01' AS VARCHAR2(30)) AS USERNAME, CAST(0 AS NUMBER) AS EXP_PP, CAST(NULL AS NUMBER) AS EXP_VA FROM DUAL UNION ALL
    SELECT 'T02', CAST(2    AS NUMBER), CAST(NULL AS NUMBER) FROM DUAL UNION ALL
    SELECT 'T03', CAST(2    AS NUMBER), CAST(NULL AS NUMBER) FROM DUAL UNION ALL
    SELECT 'T04', CAST(2    AS NUMBER), CAST(NULL AS NUMBER) FROM DUAL UNION ALL
    SELECT 'T05', CAST(2    AS NUMBER), CAST(NULL AS NUMBER) FROM DUAL UNION ALL
    SELECT 'T06', CAST(2    AS NUMBER), CAST(NULL AS NUMBER) FROM DUAL UNION ALL
    SELECT 'T07', CAST(7    AS NUMBER), CAST(NULL AS NUMBER) FROM DUAL UNION ALL
    SELECT 'T08', CAST(7    AS NUMBER), CAST(NULL AS NUMBER) FROM DUAL UNION ALL
    SELECT 'T09', CAST(0    AS NUMBER), CAST(NULL AS NUMBER) FROM DUAL UNION ALL
    SELECT 'T10', CAST(0    AS NUMBER), CAST(NULL AS NUMBER) FROM DUAL UNION ALL
    SELECT 'T11', CAST(2    AS NUMBER), CAST(NULL AS NUMBER) FROM DUAL UNION ALL
    SELECT 'T12', CAST(2    AS NUMBER), CAST(NULL AS NUMBER) FROM DUAL UNION ALL
    SELECT 'T13', CAST(NULL AS NUMBER), CAST(NULL AS NUMBER) FROM DUAL UNION ALL
    SELECT 'T14', CAST(NULL AS NUMBER), CAST(NULL AS NUMBER) FROM DUAL UNION ALL
    SELECT 'T15', CAST(NULL AS NUMBER), CAST(NULL AS NUMBER) FROM DUAL UNION ALL
    SELECT 'T16', CAST(NULL AS NUMBER), CAST(NULL AS NUMBER) FROM DUAL UNION ALL
    SELECT 'T17', CAST(NULL AS NUMBER), CAST(NULL AS NUMBER) FROM DUAL UNION ALL
    SELECT 'T18', CAST(NULL AS NUMBER), CAST(NULL AS NUMBER) FROM DUAL UNION ALL
    SELECT 'T19', CAST(2    AS NUMBER), CAST(NULL AS NUMBER) FROM DUAL UNION ALL
    SELECT 'T20', CAST(2    AS NUMBER), CAST(NULL AS NUMBER) FROM DUAL UNION ALL
    SELECT 'T21', CAST(NULL AS NUMBER), CAST(2    AS NUMBER) FROM DUAL UNION ALL
    SELECT 'T22', CAST(2    AS NUMBER), CAST(2    AS NUMBER) FROM DUAL UNION ALL
    SELECT 'T23', CAST(7    AS NUMBER), CAST(0    AS NUMBER) FROM DUAL UNION ALL
    SELECT 'T24', CAST(NULL AS NUMBER), CAST(NULL AS NUMBER) FROM DUAL UNION ALL
    SELECT 'T25', CAST(2    AS NUMBER), CAST(NULL AS NUMBER) FROM DUAL UNION ALL
    SELECT 'T26', CAST(0    AS NUMBER), CAST(NULL AS NUMBER) FROM DUAL
),
t_reg AS (
    SELECT CAST('T01' AS VARCHAR2(100)) AS USERNAME, CAST('0' AS VARCHAR2(10)) AS STATUS, CAST('PHONEPOS' AS VARCHAR2(30)) AS PARTNER FROM DUAL UNION ALL
    SELECT 'T02', '2',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T03', '3',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T04', '4',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T05', '5',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T06', '6',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T07', '7',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T08', '7',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T08', '7',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T09', '0',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T09', '7',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T10', '7',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T10', '7',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T10', '0',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T11', '2',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T11', '7',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T12', '7',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T12', '0',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T12', '3',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T13', '1',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T14', '8',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T15', '0',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T15', '1',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T16', '7',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T16', '1',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T17', 'X',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T18', '0',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T18', 'X',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T19', '2',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T19', 'X',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T20', ' 2 ', 'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T21', '3',   'VISAACCEPT' FROM DUAL UNION ALL
    SELECT 'T21', '3',   'VISAACCEPT' FROM DUAL UNION ALL
    SELECT 'T21', '0',   'VISAACCEPT' FROM DUAL UNION ALL
    SELECT 'T22', '2',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T22', '3',   'VISAACCEPT' FROM DUAL UNION ALL
    SELECT 'T23', '7',   'PHONEPOS'   FROM DUAL UNION ALL
    SELECT 'T23', '0',   'VISAACCEPT' FROM DUAL UNION ALL
    SELECT 'T25', '2',   'phonepos'   FROM DUAL UNION ALL
    SELECT ' t26 ', '0', 'PHONEPOS'   FROM DUAL
),
reg AS (
    SELECT UPPER(TRIM(r.USERNAME)) AS UNAME,
           UPPER(TRIM(r.PARTNER))  AS PARTNER,
           CASE WHEN REGEXP_LIKE(TRIM(r.STATUS), '^[+-]?[0-9]+$')
                THEN TO_NUMBER(TRIM(r.STATUS))
           END AS ST
    FROM   t_reg r
),
agg AS (
    SELECT UNAME, PARTNER,
           COUNT(*)                                       AS CNT,
           COUNT(CASE WHEN ST BETWEEN 2 AND 6 THEN 1 END) AS CNT_ACTIVE,
           COUNT(CASE WHEN ST = 7 THEN 1 END)             AS CNT_CANCEL,
           COUNT(CASE WHEN ST = 0 THEN 1 END)             AS CNT_REG
    FROM   reg GROUP BY UNAME, PARTNER
),
resolved AS (
    SELECT UNAME, PARTNER,
           CASE WHEN CNT_ACTIVE > 0             THEN 2
                WHEN CNT_CANCEL = CNT           THEN 7
                WHEN CNT_REG + CNT_CANCEL = CNT THEN 0
                ELSE NULL END AS STATUS_VAL
    FROM   agg
),
src AS (
    SELECT u.USERNAME, u.EXP_PP, u.EXP_VA,
           MAX(CASE WHEN res.PARTNER = 'PHONEPOS'   THEN res.STATUS_VAL END) AS NEW_PP,
           MAX(CASE WHEN res.PARTNER = 'VISAACCEPT' THEN res.STATUS_VAL END) AS NEW_VA
    FROM   t_users u
           LEFT JOIN resolved res ON res.UNAME = UPPER(TRIM(u.USERNAME))
    GROUP  BY u.USERNAME, u.EXP_PP, u.EXP_VA
)
SELECT COUNT(*) AS TONG_CASE,
       COUNT(CASE WHEN DECODE(NEW_PP, EXP_PP, 1, 0) = 1
                   AND DECODE(NEW_VA, EXP_VA, 1, 0) = 1 THEN 1 END) AS SO_PASS,
       COUNT(CASE WHEN DECODE(NEW_PP, EXP_PP, 1, 0) = 0
                    OR DECODE(NEW_VA, EXP_VA, 1, 0) = 0 THEN 1 END) AS SO_FAIL
FROM   src;


-- =============================================================================
-- BLOCK C - Đối chiếu trên DỮ LIỆU THẬT UAT (chỉ đọc)
-- Mỗi user: các dòng nguồn, giá trị đang có, giá trị script sẽ ghi.
-- Cột DONG_NGUON in giống log Debug của MpAppUserStatusService.
-- =============================================================================
WITH reg AS (
    SELECT UPPER(TRIM(r.USERNAME)) AS UNAME,
           UPPER(TRIM(r.PARTNER))  AS PARTNER,
           CASE WHEN REGEXP_LIKE(TRIM(r.STATUS), '^[+-]?[0-9]+$')
                THEN TO_NUMBER(TRIM(r.STATUS))
           END AS ST
    FROM   MP_APP_PARTNER_CARD_REG r
),
agg AS (
    SELECT UNAME, PARTNER,
           COUNT(*)                                       AS CNT,
           COUNT(CASE WHEN ST BETWEEN 2 AND 6 THEN 1 END) AS CNT_ACTIVE,
           COUNT(CASE WHEN ST = 7 THEN 1 END)             AS CNT_CANCEL,
           COUNT(CASE WHEN ST = 0 THEN 1 END)             AS CNT_REG
    FROM   reg GROUP BY UNAME, PARTNER
),
resolved AS (
    SELECT UNAME, PARTNER,
           CASE WHEN CNT_ACTIVE > 0             THEN 2
                WHEN CNT_CANCEL = CNT           THEN 7
                WHEN CNT_REG + CNT_CANCEL = CNT THEN 0
                ELSE NULL END AS STATUS_VAL
    FROM   agg
),
dong_nguon AS (
    SELECT UPPER(TRIM(USERNAME)) AS UNAME,
           LISTAGG(TRIM(PARTNER) || '=' || TRIM(STATUS), ' | ')
               WITHIN GROUP (ORDER BY TRIM(PARTNER), TRIM(STATUS)) AS RAW_ROWS
    FROM   MP_APP_PARTNER_CARD_REG
    GROUP  BY UPPER(TRIM(USERNAME))
)
SELECT u.USERNAME,
       NVL(d.RAW_ROWS, '(khong co dong nao)')       AS DONG_NGUON,
       NVL(TO_CHAR(u.PHONEPOS_STATUS),   'NULL')    AS PP_HIEN_TAI,
       NVL(TO_CHAR(MAX(CASE WHEN res.PARTNER = 'PHONEPOS'   THEN res.STATUS_VAL END)), 'NULL') AS PP_SE_GHI,
       NVL(TO_CHAR(u.VISAACCEPT_STATUS), 'NULL')    AS VA_HIEN_TAI,
       NVL(TO_CHAR(MAX(CASE WHEN res.PARTNER = 'VISAACCEPT' THEN res.STATUS_VAL END)), 'NULL') AS VA_SE_GHI,
       CASE WHEN DECODE(u.PHONEPOS_STATUS,   MAX(CASE WHEN res.PARTNER = 'PHONEPOS'   THEN res.STATUS_VAL END), 1, 0) = 1
             AND DECODE(u.VISAACCEPT_STATUS, MAX(CASE WHEN res.PARTNER = 'VISAACCEPT' THEN res.STATUS_VAL END), 1, 0) = 1
            THEN '-' ELSE 'DOI' END               AS THAY_DOI
FROM   MP_APP_USERS u
       LEFT JOIN resolved   res ON res.UNAME = UPPER(TRIM(u.USERNAME))
       LEFT JOIN dong_nguon d   ON d.UNAME   = UPPER(TRIM(u.USERNAME))
GROUP  BY u.USERNAME, d.RAW_ROWS, u.PHONEPOS_STATUS, u.VISAACCEPT_STATUS
ORDER  BY THAY_DOI DESC, u.USERNAME;


-- =============================================================================
-- BLOCK D - Soi dữ liệu bất thường trong UAT trước khi chạy
-- =============================================================================
-- D1. Các giá trị STATUS đang tồn tại và cách script diễn giải chúng
SELECT TRIM(STATUS) AS STATUS_RAW,
       COUNT(*)     AS SO_DONG,
       CASE WHEN NOT REGEXP_LIKE(TRIM(STATUS), '^[+-]?[0-9]+$') THEN 'KHONG PHAI SO -> bo qua, ve NULL'
            WHEN TO_NUMBER(TRIM(STATUS)) BETWEEN 2 AND 6        THEN 'Kich hoat (2)'
            WHEN TO_NUMBER(TRIM(STATUS)) = 7                    THEN 'Huy (7) neu toan bo la 7'
            WHEN TO_NUMBER(TRIM(STATUS)) = 0                    THEN 'Da dang ky (0)'
            ELSE 'KHONG THUOC NHANH NAO -> Chua dang ky (NULL)' END AS DIEN_GIAI
FROM   MP_APP_PARTNER_CARD_REG
GROUP  BY TRIM(STATUS)
ORDER  BY 1;

-- D2. Các PARTNER đang tồn tại. Giá trị ngoài PHONEPOS/VISAACCEPT sẽ bị bỏ qua.
SELECT TRIM(PARTNER) AS PARTNER_RAW, COUNT(*) AS SO_DONG
FROM   MP_APP_PARTNER_CARD_REG
GROUP  BY TRIM(PARTNER)
ORDER  BY 1;

-- D3. User có trong CARD_REG nhưng KHÔNG có trong MP_APP_USERS -> script bỏ qua
--     (service cũng chỉ log warning). Kiểm tra xem có phải dữ liệu rác không.
SELECT DISTINCT r.USERNAME
FROM   MP_APP_PARTNER_CARD_REG r
WHERE  NOT EXISTS (SELECT 1 FROM MP_APP_USERS u
                   WHERE UPPER(TRIM(u.USERNAME)) = UPPER(TRIM(r.USERNAME)))
ORDER  BY 1;

-- D4. Username trùng nhau sau khi UPPER/TRIM trong MP_APP_USERS (nếu có, 2 dòng
--     này sẽ cùng nhận một giá trị — cần xác nhận là ý muốn).
SELECT UPPER(TRIM(USERNAME)) AS UNAME, COUNT(*) AS SO_DONG
FROM   MP_APP_USERS
GROUP  BY UPPER(TRIM(USERNAME))
HAVING COUNT(*) > 1;

-- D5. User đang có PHONEPOS/VISAACCEPT khác NULL nhưng KHÔNG có dòng CARD_REG nào
--     -> script sẽ set về NULL. Nếu danh sách này không rỗng, cân nhắc thêm
--     điều kiện EXISTS vào script update (xem ghi chú trong file update).
SELECT u.USERNAME, u.PHONEPOS_STATUS, u.VISAACCEPT_STATUS
FROM   MP_APP_USERS u
WHERE  (u.PHONEPOS_STATUS IS NOT NULL OR u.VISAACCEPT_STATUS IS NOT NULL)
  AND  NOT EXISTS (SELECT 1 FROM MP_APP_PARTNER_CARD_REG r
                   WHERE UPPER(TRIM(r.USERNAME)) = UPPER(TRIM(u.USERNAME)))
ORDER  BY 1;
