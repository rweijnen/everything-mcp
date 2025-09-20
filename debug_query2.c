#include <windows.h>
#include <stdio.h>

// Copy the Everything IPC structures
#define EVERYTHING_IPC_COPYDATAQUERYW       1
#define EVERYTHING_IPC_COPYDATA_QUERY2W     3
#define EVERYTHING_IPC_COPYDATA_QUERYCOMPLETE 0

#define EVERYTHING_IPC_ALLRESULTS           0xFFFFFFFF

typedef struct
{
    DWORD reply_hwnd;
    DWORD reply_copydata_message;
    DWORD search_flags;
    DWORD offset;
    DWORD max_results;
    DWORD request_flags;
    DWORD sort_type;
    WCHAR search_string[1];
} EVERYTHING_IPC_QUERY2W;

typedef struct
{
    DWORD flags;
    DWORD data_offset;
} EVERYTHING_IPC_ITEM2W;

typedef struct
{
    DWORD totitems;
    DWORD numitems;
    DWORD offset;
    DWORD request_flags;
    DWORD sort_type;
} EVERYTHING_IPC_LIST2W;

// Request flags from Everything
#define EVERYTHING_IPC_QUERY2_REQUEST_NAME                  0x00000001
#define EVERYTHING_IPC_QUERY2_REQUEST_PATH                  0x00000002
#define EVERYTHING_IPC_QUERY2_REQUEST_FULL_PATH_AND_NAME    0x00000004
#define EVERYTHING_IPC_QUERY2_REQUEST_EXTENSION             0x00000008
#define EVERYTHING_IPC_QUERY2_REQUEST_SIZE                  0x00000010
#define EVERYTHING_IPC_QUERY2_REQUEST_DATE_CREATED          0x00000020
#define EVERYTHING_IPC_QUERY2_REQUEST_DATE_MODIFIED         0x00000040
#define EVERYTHING_IPC_QUERY2_REQUEST_DATE_ACCESSED         0x00000080
#define EVERYTHING_IPC_QUERY2_REQUEST_ATTRIBUTES            0x00000100
#define EVERYTHING_IPC_QUERY2_REQUEST_RUN_COUNT             0x00001000
#define EVERYTHING_IPC_QUERY2_REQUEST_DATE_RUN              0x00002000

int main()
{
    printf("Everything IPC QUERY2 Structure Analysis\n");
    printf("========================================\n\n");

    printf("Structure sizes:\n");
    printf("EVERYTHING_IPC_QUERY2W: %zu bytes\n", sizeof(EVERYTHING_IPC_QUERY2W));
    printf("EVERYTHING_IPC_LIST2W: %zu bytes\n", sizeof(EVERYTHING_IPC_LIST2W));
    printf("EVERYTHING_IPC_ITEM2W: %zu bytes\n", sizeof(EVERYTHING_IPC_ITEM2W));
    printf("\n");

    printf("Field offsets in EVERYTHING_IPC_QUERY2W:\n");
    printf("reply_hwnd: %zu\n", offsetof(EVERYTHING_IPC_QUERY2W, reply_hwnd));
    printf("reply_copydata_message: %zu\n", offsetof(EVERYTHING_IPC_QUERY2W, reply_copydata_message));
    printf("search_flags: %zu\n", offsetof(EVERYTHING_IPC_QUERY2W, search_flags));
    printf("offset: %zu\n", offsetof(EVERYTHING_IPC_QUERY2W, offset));
    printf("max_results: %zu\n", offsetof(EVERYTHING_IPC_QUERY2W, max_results));
    printf("request_flags: %zu\n", offsetof(EVERYTHING_IPC_QUERY2W, request_flags));
    printf("sort_type: %zu\n", offsetof(EVERYTHING_IPC_QUERY2W, sort_type));
    printf("search_string: %zu\n", offsetof(EVERYTHING_IPC_QUERY2W, search_string));
    printf("\n");

    printf("Field offsets in EVERYTHING_IPC_LIST2W:\n");
    printf("totitems: %zu\n", offsetof(EVERYTHING_IPC_LIST2W, totitems));
    printf("numitems: %zu\n", offsetof(EVERYTHING_IPC_LIST2W, numitems));
    printf("offset: %zu\n", offsetof(EVERYTHING_IPC_LIST2W, offset));
    printf("request_flags: %zu\n", offsetof(EVERYTHING_IPC_LIST2W, request_flags));
    printf("sort_type: %zu\n", offsetof(EVERYTHING_IPC_LIST2W, sort_type));
    printf("\n");

    printf("Field offsets in EVERYTHING_IPC_ITEM2W:\n");
    printf("flags: %zu\n", offsetof(EVERYTHING_IPC_ITEM2W, flags));
    printf("data_offset: %zu\n", offsetof(EVERYTHING_IPC_ITEM2W, data_offset));
    printf("\n");

    printf("Request flag values:\n");
    printf("NAME: 0x%08X\n", EVERYTHING_IPC_QUERY2_REQUEST_NAME);
    printf("PATH: 0x%08X\n", EVERYTHING_IPC_QUERY2_REQUEST_PATH);
    printf("FULL_PATH_AND_NAME: 0x%08X\n", EVERYTHING_IPC_QUERY2_REQUEST_FULL_PATH_AND_NAME);
    printf("SIZE: 0x%08X\n", EVERYTHING_IPC_QUERY2_REQUEST_SIZE);
    printf("DATE_CREATED: 0x%08X\n", EVERYTHING_IPC_QUERY2_REQUEST_DATE_CREATED);
    printf("DATE_MODIFIED: 0x%08X\n", EVERYTHING_IPC_QUERY2_REQUEST_DATE_MODIFIED);
    printf("DATE_ACCESSED: 0x%08X\n", EVERYTHING_IPC_QUERY2_REQUEST_DATE_ACCESSED);
    printf("ATTRIBUTES: 0x%08X\n", EVERYTHING_IPC_QUERY2_REQUEST_ATTRIBUTES);

    return 0;
}