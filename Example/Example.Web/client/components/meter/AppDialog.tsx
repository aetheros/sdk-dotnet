import Button from '@material-ui/core/Button';
import Dialog from "@material-ui/core/Dialog";
import DialogActions from '@material-ui/core/DialogActions';
import DialogContent from '@material-ui/core/DialogContent';
import DialogTitle from '@material-ui/core/DialogTitle';
import * as React from "react";

type Props = {
	title: string;
	open: boolean;
	onClose: React.ReactEventHandler;
	onSave: React.ReactEventHandler;
	content: JSX.Element;
};

type State = {
	open: boolean;
}

export default class AppDialog extends React.Component<Props, State> {

	constructor(props) {
		super(props);
		this.state = {
			open: props.open,
		};
	}

	render() {
		return (
			<Dialog
				open={this.props.open}
				onClose={this.props.onClose.bind(this)}
				aria-labelledby="form-dialog-title"
			>
				<DialogTitle>{this.props.title}</DialogTitle>
				<DialogContent>
					{this.props.content}
				</DialogContent>
				<DialogActions>
					<Button onClick={this.props.onClose.bind(this)}>
						Cancel
					</Button>
					<Button onClick={this.props.onSave.bind(this)} color="primary" variant="contained">
						Save
					</Button>
				</DialogActions>
			</Dialog>
		);
	}
}