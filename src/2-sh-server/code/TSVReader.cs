using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using MemberTypes = CsvHelper.Configuration.MemberTypes;

namespace Jay.FileIO
{
    /// <summary>
    /// 순수 C# 서버 환경용 TSV 리더.
    /// UnityEngine 의존성을 제거하고, 로깅은 콜백(Action)으로 위임합니다.
    /// </summary>
    public static class TSVReader
    {
        private static readonly CsvConfiguration TsvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = "\t",
            Mode = CsvMode.NoEscape,
            HasHeaderRecord = true,
            MissingFieldFound = null,
            HeaderValidated = null,
            // 프로퍼티뿐 아니라 public 필드도 헤더에 매핑한다.
            MemberTypes = MemberTypes.Properties | MemberTypes.Fields,
        };

        /// <summary>
        /// 에러 발생 시 호출되는 로그 콜백. 기본값은 Console.Error.
        /// 서버 환경의 로깅 프레임워크(ILogger 등)와 연결하고 싶다면 이 값을 교체하세요.
        /// </summary>
        public static Action<string> OnError = msg => Console.Error.WriteLine(msg);

        /// <summary>
        /// 지정된 폴더에서 tableName.tsv 파일을 읽어 List&lt;T&gt;로 반환합니다.
        /// </summary>
        /// <typeparam name="T">매핑할 클래스 타입 (public getter/setter 또는 필드 필수)</typeparam>
        /// <param name="baseFolderPath">테이블 파일들이 위치한 폴더 경로</param>
        /// <param name="tableName">파일 이름 (확장자 제외)</param>
        public static async Task<List<T>> ReadTableAsync<T>(string baseFolderPath, string tableName)
        {
            string filePath = Path.Combine(baseFolderPath, tableName + ".tsv");

            if (!File.Exists(filePath))
            {
                OnError?.Invoke($"[TSVReader] 파일이 존재하지 않습니다: {filePath}");
                return null;
            }

            try
            {
                using var reader = new StreamReader(filePath);
                using var csv = new CsvReader(reader, TsvConfig);

                var records = new List<T>();
                await foreach (var record in csv.GetRecordsAsync<T>())
                {
                    records.Add(record);
                }

                return records;
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"[TSVReader] {tableName}.tsv 로딩 실패: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 전체 파일 경로로 TSV를 읽어 List&lt;T&gt;로 반환합니다. (동기)
        /// </summary>
        public static List<T> ReadTable<T>(string filePath)
        {
            if (!File.Exists(filePath))
            {
                OnError?.Invoke($"[TSVReader] 파일이 존재하지 않습니다: {filePath}");
                OnError?.Invoke($"[TSVReader] 경로 존재 여부: {File.Exists(filePath)}");
            }

                using var reader = new StreamReader(filePath);
                using var csv = new CsvReader(reader, TsvConfig);

                return csv.GetRecords<T>().ToList();

        }

        /// <summary>
        /// 전체 파일 경로로 TSV를 읽어 List&lt;T&gt;로 반환합니다. (비동기)
        /// </summary>
        public static async Task<List<T>> ReadTableAsync<T>(string filePath)
        {
            if (!File.Exists(filePath))
            {
                OnError?.Invoke($"[TSVReader] 파일이 존재하지 않습니다: {filePath}");
                return null;
            }

            try
            {
                using var reader = new StreamReader(filePath);
                using var csv = new CsvReader(reader, TsvConfig);

                var records = new List<T>();
                await foreach (var record in csv.GetRecordsAsync<T>())
                {
                    records.Add(record);
                }

                return records;
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"[TSVReader] {filePath} 로딩 실패: {ex.Message}");
                return null;
            }
        }
    }
}