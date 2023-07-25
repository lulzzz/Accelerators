import React, { useState, useEffect } from 'react';
import { BlobServiceClient, AnonymousCredential  } from "@azure/storage-blob";
import './styles.css';

const MyTable = () => {
  const [userName, setUserName] = useState('');
  const [question, setQuestion] = useState('');
  const [answer, setAnswer] = useState('');
  const [historyList, setHistoryList] = useState([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    console.log("The Answer: " + answer);
  }, [answer]);

  const handleUserNameChange = (event) => {
    setUserName(event.target.value);
  };

  const handleQuestionChange = (event) => {
    setQuestion(event.target.value);
  };

  const  handleAskQuestion = async () => {
    setLoading(true);
    const url = 'http://localhost:7101/api/openAI'; //'https://orchestartorapi.azurewebsites.net/api/openAI';
    console.log("Calling");
    const response = await fetch(url, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        //'x-functions-key': '',
      },
      body: JSON.stringify({
        guid: userName,
        question: question
      }),
    });
    if (response.ok){
      const data = await response.text();
      setAnswer(data);      
      setHistoryList(prevHistory => [...prevHistory, { question, answer: data }]);
       setQuestion('');
    }
    setLoading(false);
    
  };
  const [selectedFile, setSelectedFile] = useState(null);

  const handleFileChange = (event) => {
    setSelectedFile(event.target.files[0]);
  };

  const handleUpload = async () => {
    if (!selectedFile) {
      alert('Please select a file to upload.');
      return;
    }

    // Use the SAS token in the URL to authenticate the request
    const sasToken = '?sv=2022-11-02&ss=bfqt&srt=co&sp=rwdlacupiytfx&se=2023-07-25T22:06:44Z&st=2023-07-25T14:06:44Z&spr=https&sig=e1%2FZ1Te%2F4%2FVj5qV9sd1%2F99Q0zEfHOjU5kccSHU0ror8%3D'; // Replace with the SAS token obtained from the server-side

    // Initialize the BlobServiceClient with an AnonymousCredential (useful for browser environments)
    const blobServiceClient = new BlobServiceClient(
      `https://docfileprocessor.blob.core.windows.net?${sasToken}`,
      new AnonymousCredential()
    );

    const containerName = 'input';
    const containerClient = blobServiceClient.getContainerClient(containerName);
    const blockBlobClient = containerClient.getBlockBlobClient(selectedFile.name);

    try {
      await blockBlobClient.uploadData(selectedFile, {
        blobHTTPHeaders: { blobContentType: selectedFile.type },
      });
      alert('File uploaded successfully!');
    } catch (error) {
      console.error('Error uploading file:', error);
      alert('An error occurred while uploading the file.');
    }
  };

  return (
    <div>
      <br></br>
    <div className="container">
      <h1>Ask a Question</h1>
      <div className="form-group">
        <label htmlFor="userName">User:</label>
        <input type="text" id="userName" value={userName} onChange={handleUserNameChange} />
      </div>
      <div className="form-group">
        <label htmlFor="question">Question:</label>
        <textarea id="question" value={question} onChange={handleQuestionChange} rows={4} />
      </div>
      <button className="btn" onClick={handleAskQuestion} disabled={loading}>
        {loading ? 'Thinking...' : 'Ask Question'}
      </button>
     
      {historyList.length > 0 && (
        <>
          <h2>Results</h2>
          <div className="result-container">
  {historyList.map((entry, index) => (
    <div key={index} className="message-bubble">
      <div className="question-bubble">{entry.question}</div>
      <div className="answer-bubble" dangerouslySetInnerHTML={{ __html: entry.answer }}></div>
    </div>
  ))}
</div>

        </>
      )}
      <hr />
      <h1>Upload File(s)</h1>
      <div>
      <input type="file" onChange={handleFileChange} />
      <button onClick={handleUpload}>Upload</button>
    </div>
    </div></div>
  );
};

export default MyTable;
