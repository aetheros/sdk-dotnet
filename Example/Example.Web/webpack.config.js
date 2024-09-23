'use strict';

const MiniCssExtractPlugin = require('mini-css-extract-plugin');
//const UglifyJsPlugin = require('uglifyjs-webpack-plugin');

/*
module.exports = {
	mode: 'development',
	entry: {
		app: './client/app.tsx'
	},
	output: {
		path: __dirname + '/wwwroot/dist',
		publicPath: '/dist/'
	},
	resolve: {
		modules: ['client', 'node_modules'],
		extensions: ['.ts', '.tsx', '.js', '.jsx']
	},
	devtool: 'source-map',
	module: {
		rules: [
			{ test: /\.jsx?$/, use: 'babel-loader', exclude: /node_modules/ },
			{ test: /\.tsx?$/, use: 'awesome-typescript-loader?silent=true' },
			{ test: /\.css$/, use: [MiniCssExtractPlugin.loader, 'css-loader?minimize'] },
			{ test: /\.svg$/, use: 'svg-url-loader?noquotes=true' },
			{ test: /\.(png|jpg|jpeg|gif)$/, use: 'url-loader?limit=25000' }
		]
	},
	plugins: [
		new MiniCssExtractPlugin()
		//new UglifyJsPlugin()
	]
};
*/

module.exports = {
	stats: {
		all: true,
		errors: true,
		warnings: true,
	},
	mode: 'development',
	entry: {
		app: './client/app.tsx'
	},
	output: {
		path: __dirname + '/wwwroot/dist',
		publicPath: '/dist/'
	},
	resolve: {
		modules: ['client', 'node_modules'],
		extensions: ['.ts', '.tsx', '.js', '.jsx']
	},
	devtool: 'source-map',
	module: {
		rules: [
			{ test: /\.jsx?$/, use: 'babel-loader', exclude: /node_modules/ },
			{ test: /\.tsx?$/, use: 'ts-loader', exclude: /node_modules/ }, // Changed to ts-loader
			{
				test: /\.css$/,
				use: [
					MiniCssExtractPlugin.loader,
					{
						loader: 'css-loader',
						options: { minimize: true }
					}
				]
			},
			{ test: /\.svg$/, use: 'svg-url-loader?noquotes=true' },
			{ test: /\.(png|jpg|jpeg|gif)$/, use: 'url-loader?limit=25000' }
		]
	},
	plugins: [
		new MiniCssExtractPlugin()
	]
};
