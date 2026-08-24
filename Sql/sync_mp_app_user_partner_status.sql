-- Dong bo MP_APP_USERS.PHONEPOS_STATUS / VISAACCEPT_STATUS tu MP_APP_PARTNER_CARD_REG.
-- Logic bam theo MpAppUserStatusService.ResolveStatus:
--   >= 1 ban ghi trang thai 2/3/4/5/6  -> 2    (Kich hoat)
--   tat ca ban ghi trang thai 7        -> 7    (Huy)
--   tat ca ban ghi trang thai 0 / 0&7  -> 0    (Da dang ky)
--   con lai, ke ca khong co ban ghi    -> NULL (Chua dang ky)
-- Thu tu nhanh bat buoc: co ca 2 lan 7 phai ra Kich hoat. Khong dung FINONE_STATUS.
--
-- Ghi chu cu phap, de tranh ORA-00905:
--   1. Khong dung WITH ben trong USING(...) -> viet bang inline view long nhau.
--   2. Khong dat WHERE sau WHEN MATCHED THEN UPDATE SET -> loc "chi dong thay doi"
--      nam trong subquery S.
--   3. Khong de comment nam trong long cau lenh.
-- Chay cau lenh nay trong mot worksheet TRONG. Neu phia tren con cau lenh khac
-- chua ket thuc bang dau ';', parser se gop hai cau lam mot va bao ORA-00905.

MERGE INTO MP_APP_USERS TGT
USING (
    SELECT S.USERNAME,
           S.NEW_PHONEPOS,
           S.NEW_VISA
    FROM (
        SELECT U.USERNAME          AS USERNAME,
               U.PHONEPOS_STATUS   AS OLD_PHONEPOS,
               U.VISAACCEPT_STATUS AS OLD_VISA,
               MAX(CASE WHEN RES.PARTNER = 'PHONEPOS'   THEN RES.STATUS_VAL END) AS NEW_PHONEPOS,
               MAX(CASE WHEN RES.PARTNER = 'VISAACCEPT' THEN RES.STATUS_VAL END) AS NEW_VISA
        FROM   MP_APP_USERS U
               LEFT JOIN (
                   SELECT AGG.UNAME,
                          AGG.PARTNER,
                          CASE
                              WHEN AGG.CNT_ACTIVE > 0                     THEN 2
                              WHEN AGG.CNT_CANCEL = AGG.CNT               THEN 7
                              WHEN AGG.CNT_REG + AGG.CNT_CANCEL = AGG.CNT THEN 0
                              ELSE NULL
                          END AS STATUS_VAL
                   FROM (
                       SELECT REG.UNAME,
                              REG.PARTNER,
                              COUNT(*)                                           AS CNT,
                              COUNT(CASE WHEN REG.ST BETWEEN 2 AND 6 THEN 1 END) AS CNT_ACTIVE,
                              COUNT(CASE WHEN REG.ST = 7 THEN 1 END)             AS CNT_CANCEL,
                              COUNT(CASE WHEN REG.ST = 0 THEN 1 END)             AS CNT_REG
                       FROM (
                           SELECT UPPER(TRIM(R.USERNAME)) AS UNAME,
                                  UPPER(TRIM(R.PARTNER))  AS PARTNER,
                                  CASE WHEN REGEXP_LIKE(TRIM(R.STATUS), '^[+-]?[0-9]+$')
                                       THEN TO_NUMBER(TRIM(R.STATUS))
                                  END AS ST
                           FROM   MP_APP_PARTNER_CARD_REG R
                       ) REG
                       GROUP BY REG.UNAME, REG.PARTNER
                   ) AGG
               ) RES ON RES.UNAME = UPPER(TRIM(U.USERNAME))
        GROUP  BY U.USERNAME, U.PHONEPOS_STATUS, U.VISAACCEPT_STATUS
    ) S
    WHERE  DECODE(S.OLD_PHONEPOS, S.NEW_PHONEPOS, 1, 0) = 0
       OR  DECODE(S.OLD_VISA,     S.NEW_VISA,     1, 0) = 0
) SRC
ON (TGT.USERNAME = SRC.USERNAME)
WHEN MATCHED THEN UPDATE SET
       TGT.PHONEPOS_STATUS   = SRC.NEW_PHONEPOS,
       TGT.VISAACCEPT_STATUS = SRC.NEW_VISA;

-- Xem so dong anh huong roi COMMIT;
