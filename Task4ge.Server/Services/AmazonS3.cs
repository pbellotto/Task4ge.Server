/*
 * Copyright (C) 2024 pbellotto (pedro.augusto.bellotto@gmail.com)
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;

namespace Task4ge.Server.Services;

public static class AmazonS3
{
    private const string BUCKET_NAME = "task4gebucket";

    public static async Task<string> UploadImageAsync(IAmazonS3 client, Stream image, string contentType)
    {
        TransferUtility fileTransferUtility = new(client);
        string key = Guid.NewGuid().ToString();
        await fileTransferUtility.UploadAsync(
            new()
            {
                BucketName = BUCKET_NAME,
                Key = key,
                InputStream = image,
                ContentType = contentType,
                CannedACL = S3CannedACL.PublicRead
            });
        return $"https://task4gebucket.s3.amazonaws.com/{key}";
    }

    public static async Task DeleteImageAsync(IAmazonS3 client, string key)
    {
        await client.DeleteObjectAsync(
            new DeleteObjectRequest
            {
                BucketName = BUCKET_NAME,
                Key = key
            });
    }
}