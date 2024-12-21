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

public sealed class AmazonS3Api(IAmazonS3 client) : IAmazonS3Api
{
    private const string BUCKET_NAME = "task4gebucket";

    private readonly IAmazonS3 _client = client;

    public async Task<(string, string)> UploadImageAsync(Stream image, string contentType)
    {
        TransferUtility fileTransferUtility = new(_client);
        string key = Guid.NewGuid().ToString();
        await fileTransferUtility.UploadAsync(
            new()
            {
                BucketName = BUCKET_NAME,
                Key = key,
                InputStream = image,
                ContentType = contentType,
                CannedACL = S3CannedACL.PublicRead,
            });
        return (key, $"https://task4gebucket.s3.amazonaws.com/{key}");
    }

    public async Task DeleteImageAsync(string key)
    {
        await _client.DeleteObjectAsync(
            new DeleteObjectRequest
            {
                BucketName = BUCKET_NAME,
                Key = key
            });
    }
}